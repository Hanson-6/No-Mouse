using UnityEngine;

/// <summary>
/// 在两个端点之间来回移动的平台。
/// 初始位置可以不在 pointA/pointB：
///   - startAtPointB = false → 先移动到 pointA，再开始 A↔B 循环
///   - startAtPointB = true  → 先移动到 pointB，再开始 B↔A 循环
/// 玩家站上去会随平台移动。
///
/// 当 buttonControlled = true 时，平台不会自动巡逻，而是停在 pointA，
/// 等待 ButtonController 调用 Activate() 移动到 pointB，
/// Deactivate() 返回 pointA。
/// </summary>
public class MovingPlatform : MonoBehaviour, ISnapshotSaveable, IButtonActivatable
{
    [Header("Movement")]
    public Vector2 pointA;
    public Vector2 pointB;
    public float speed = 2f;
    [Tooltip("If true, platform moves to Point B first, then patrols B↔A")]
    public bool startAtPointB = false;

    [Header("Button Control")]
    [Tooltip("If true, platform stays at pointA and only moves to pointB when activated by a ButtonController")]
    public bool buttonControlled = false;

    [Header("Pause")]
    [Tooltip("How long the platform pauses at each endpoint (seconds)")]
    public float waitTime = 1f;

    private enum PatrolState { MovingToStart, Patrolling }
    private PatrolState state;
    private bool goingToB;   // only used during Patrolling
    private float waitTimer;
    private Vector3 lastPosition;
    private bool activated;  // used only in buttonControlled mode

    public float CurrentVelocityX { get; private set; }

    [System.Serializable]
    private class SnapshotState
    {
        public float positionX;
        public float positionY;
        public float positionZ;
        public int patrolState;
        public bool patrolToB;
        public float waitTimer;
        public bool activated;
    }

    void Start()
    {
        // Default points if not set in Inspector
        if (pointA == Vector2.zero && pointB == Vector2.zero)
        {
            pointA = (Vector2)transform.position;
            pointB = pointA + Vector2.right * 5f;
        }

        if (buttonControlled)
        {
            // Button-controlled mode: start at pointA, wait for Activate()
            transform.position = new Vector3(pointA.x, pointA.y, transform.position.z);
            state = PatrolState.Patrolling;
            goingToB = false;
            activated = false;
            waitTimer = 0f;
        }
        else
        {
            Vector2 startPoint = startAtPointB ? pointB : pointA;
            float distToStart = Vector2.Distance(transform.position, startPoint);

            if (distToStart < 0.05f)
            {
                // Already at the start point — go straight into patrol
                transform.position = new Vector3(startPoint.x, startPoint.y, transform.position.z);
                state = PatrolState.Patrolling;
                goingToB = !startAtPointB; // from A→go to B, from B→go to A
                waitTimer = waitTime;      // initial pause
            }
            else
            {
                // Need to travel to the start point first
                state = PatrolState.MovingToStart;
                waitTimer = 0f;
            }
        }

        lastPosition = transform.position;
        CurrentVelocityX = 0f;
    }

    void Update()
    {
        if (buttonControlled)
        {
            UpdateButtonControlled();
        }
        else
        {
            UpdatePatrol();
        }

        float dt = Time.deltaTime;
        if (dt > 0f)
            CurrentVelocityX = (transform.position.x - lastPosition.x) / dt;
        else
            CurrentVelocityX = 0f;

        lastPosition = transform.position;
    }

    // --- IButtonActivatable ---

    /// <summary>Move toward pointB (button pressed).</summary>
    public void Activate()
    {
        activated = true;
    }

    /// <summary>Return to pointA (button released).</summary>
    public void Deactivate()
    {
        activated = false;
    }

    private void UpdateButtonControlled()
    {
        Vector2 target = activated ? pointB : pointA;
        if (Vector2.Distance(transform.position, target) < 0.01f)
        {
            transform.position = new Vector3(target.x, target.y, transform.position.z);
            return;
        }
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    private void UpdatePatrol()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        if (state == PatrolState.MovingToStart)
        {
            Vector2 startPoint = startAtPointB ? pointB : pointA;
            transform.position = Vector2.MoveTowards(transform.position, startPoint, speed * Time.deltaTime);

            if (Vector2.Distance(transform.position, startPoint) < 0.05f)
            {
                transform.position = new Vector3(startPoint.x, startPoint.y, transform.position.z);
                state = PatrolState.Patrolling;
                goingToB = !startAtPointB;
                waitTimer = waitTime;
            }
        }
        else // Patrolling
        {
            Vector2 target = goingToB ? pointB : pointA;
            transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (Vector2.Distance(transform.position, target) < 0.05f)
            {
                transform.position = new Vector3(target.x, target.y, transform.position.z);
                goingToB = !goingToB;
                waitTimer = waitTime;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            col.gameObject.GetComponent<PlayerController>()?.SetCurrentPlatform(this);
        }
        else if (col.gameObject.CompareTag("Box"))
        {
            col.gameObject.GetComponent<PushableBox>()?.SetCurrentPlatform(this);
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            col.gameObject.GetComponent<PlayerController>()?.ClearCurrentPlatform(this);
        }
        else if (col.gameObject.CompareTag("Box"))
        {
            col.gameObject.GetComponent<PushableBox>()?.ClearCurrentPlatform(this);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 a = pointA == Vector2.zero ? (Vector2)transform.position : pointA;
        Vector2 b = pointB == Vector2.zero ? (Vector2)transform.position + Vector2.right * 5f : pointB;

        // Patrol range
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(a, 0.1f);
        Gizmos.DrawSphere(b, 0.1f);
        Gizmos.DrawLine(a, b);

        // Path from spawn to start point
        Vector2 startPoint = startAtPointB ? b : a;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, startPoint);
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            positionX = transform.position.x,
            positionY = transform.position.y,
            positionZ = transform.position.z,
            patrolState = (int)state,
            patrolToB = goingToB,
            waitTimer = waitTimer,
            activated = activated
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState data = JsonUtility.FromJson<SnapshotState>(stateJson);
        transform.position = new Vector3(data.positionX, data.positionY, data.positionZ);

        int maxEnumValue = (int)PatrolState.Patrolling;
        int clampedState = Mathf.Clamp(data.patrolState, 0, maxEnumValue);
        state = (PatrolState)clampedState;
        goingToB = data.patrolToB;
        waitTimer = Mathf.Max(0f, data.waitTimer);
        activated = data.activated;

        lastPosition = transform.position;
        CurrentVelocityX = 0f;
    }
}
