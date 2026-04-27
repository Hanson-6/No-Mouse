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

    [Tooltip("Optional: in patrol mode the platform reverses direction when blocked by this door's collider. In button-controlled mode the platform stays at pointA while the door is closed.")]
    [SerializeField] private SwitchDoor doorBlocker;

    [Header("Pause")]
    [Tooltip("How long the platform pauses at each endpoint (seconds)")]
    public float waitTime = 1f;

    private enum PatrolState { MovingToStart, Patrolling }
    private PatrolState state;
    private bool goingToB;   // only used during Patrolling
    private float waitTimer;
    private Rigidbody2D rb;
    private bool activated;  // used only in buttonControlled mode
    private bool isBlockedByDoor;
    private bool wasBlockedByDoor;

    public Vector2 CurrentVelocity { get; private set; }
    public float CurrentVelocityX => CurrentVelocity.x;
    public float CurrentVelocityY => CurrentVelocity.y;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.useFullKinematicContacts = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
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
            SetPlatformPositionImmediate(pointA);
            state = PatrolState.Patrolling;
            goingToB = false;
            activated = false;
            waitTimer = 0f;
        }
        else
        {
            Vector2 startPoint = startAtPointB ? pointB : pointA;
            float distToStart = Vector2.Distance(GetPlatformPosition(), startPoint);

            if (distToStart < 0.05f)
            {
                // Already at the start point — go straight into patrol
                SetPlatformPositionImmediate(startPoint);
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

        CurrentVelocity = Vector2.zero;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 currentPosition = GetPlatformPosition();
        Vector2 nextPosition;

        if (buttonControlled)
        {
            nextPosition = UpdateButtonControlled(currentPosition, dt);
        }
        else
        {
            // Physics-based door blocking: if the door collider just now
            // blocked the platform, reverse patrol direction.
            if (isBlockedByDoor && !wasBlockedByDoor)
            {
                goingToB = !goingToB;
                waitTimer = 0f;
            }
            wasBlockedByDoor = isBlockedByDoor;
            nextPosition = UpdatePatrol(currentPosition, dt);
        }

        if (dt > 0f)
        {
            CurrentVelocity = (nextPosition - currentPosition) / dt;
        }
        else
        {
            CurrentVelocity = Vector2.zero;
        }

        MovePlatformTo(nextPosition);
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

    private Vector2 UpdateButtonControlled(Vector2 currentPosition, float dt)
    {
        // If a door is blocking the path and it is not yet open, keep the platform at pointA.
        bool blockedByDoor = doorBlocker != null && !doorBlocker.IsOpen;

        Vector2 target = (activated && !blockedByDoor) ? pointB : pointA;
        if (Vector2.Distance(currentPosition, target) < 0.01f)
        {
            return target;
        }

        return Vector2.MoveTowards(currentPosition, target, speed * dt);
    }

    private Vector2 UpdatePatrol(Vector2 currentPosition, float dt)
    {
        if (waitTimer > 0f)
        {
            waitTimer -= dt;
            return currentPosition;
        }

        if (state == PatrolState.MovingToStart)
        {
            Vector2 startPoint = startAtPointB ? pointB : pointA;
            Vector2 nextPosition = Vector2.MoveTowards(currentPosition, startPoint, speed * dt);

            if (Vector2.Distance(nextPosition, startPoint) < 0.05f)
            {
                nextPosition = startPoint;
                state = PatrolState.Patrolling;
                goingToB = !startAtPointB;
                waitTimer = waitTime;
            }

            return nextPosition;
        }
        else // Patrolling
        {
            Vector2 target = goingToB ? pointB : pointA;
            Vector2 nextPosition = Vector2.MoveTowards(currentPosition, target, speed * dt);

            if (Vector2.Distance(nextPosition, target) < 0.05f)
            {
                nextPosition = target;
                goingToB = !goingToB;
                waitTimer = waitTime;
            }

            return nextPosition;
        }
    }

    private Vector2 GetPlatformPosition()
    {
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    private void MovePlatformTo(Vector2 position)
    {
        if (rb != null)
            rb.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void SetPlatformPositionImmediate(Vector2 position)
    {
        if (rb != null)
            rb.position = position;

        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        UpdatePlatformContact(col, true);
        CheckDoorCollision(col, true);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        // Important: objects can start the scene already touching the platform.
        // In that case OnCollisionEnter2D can be skipped, so we refresh contact
        // continuously here to keep Player/Box bound to this platform.
        UpdatePlatformContact(col, true);
        CheckDoorCollision(col, true);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        UpdatePlatformContact(col, false);
        CheckDoorCollision(col, false);
    }

    private void CheckDoorCollision(Collision2D col, bool touching)
    {
        if (doorBlocker == null) return;
        if (col.gameObject == doorBlocker.gameObject)
        {
            isBlockedByDoor = touching;
        }
    }

    private void UpdatePlatformContact(Collision2D col, bool touching)
    {
        if (col == null)
            return;

        GameObject other = col.gameObject;
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (touching) player?.SetCurrentPlatform(this);
            else player?.ClearCurrentPlatform(this);
        }
        else if (other.CompareTag("Box"))
        {
            PushableBox box = other.GetComponent<PushableBox>();
            if (touching) box?.SetCurrentPlatform(this);
            else box?.ClearCurrentPlatform(this);
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
        Vector2 platformPosition = GetPlatformPosition();
        var snapshot = new SnapshotState
        {
            positionX = platformPosition.x,
            positionY = platformPosition.y,
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
        SetPlatformPositionImmediate(new Vector2(data.positionX, data.positionY));
        transform.position = new Vector3(data.positionX, data.positionY, data.positionZ);

        int maxEnumValue = (int)PatrolState.Patrolling;
        int clampedState = Mathf.Clamp(data.patrolState, 0, maxEnumValue);
        state = (PatrolState)clampedState;
        goingToB = data.patrolToB;
        waitTimer = Mathf.Max(0f, data.waitTimer);
        activated = data.activated;

        CurrentVelocity = Vector2.zero;
    }
}
