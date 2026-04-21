using UnityEngine;
using System.Collections;

/// <summary>
/// 基础巡逻敌人。
/// - 先从初始位置移动到 pointA，然后在 pointA 和 pointB 之间来回巡逻
/// - 玩家从上方踩踏 → 敌人死亡
/// - 玩家侧面接触 → 玩家死亡
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, ISnapshotSaveable
{
    [Header("Patrol")]
    public float moveSpeed = 2f;
    [Tooltip("Left boundary of patrol range")]
    public Vector2 pointA;
    [Tooltip("Right boundary of patrol range")]
    public Vector2 pointB;

    [Header("Stomp")]
    public float stompThreshold = 0.2f; // 玩家速度 y 低于此值才算踩踏

    [Header("Audio")]
    [SerializeField] private AudioClip deathSound;

    [Header("Death")]
    [SerializeField] private float hideAfterDeathDelay = 0.5f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator animator;
    private bool isDead = false;

    // State machine
    private enum PatrolState { MovingToA, PatrolAB }
    private PatrolState state;
    private bool goingToB; // only used in PatrolAB state
    private Coroutine hideRoutine;

    [System.Serializable]
    private class SnapshotState
    {
        public bool activeSelf;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float velocityX;
        public float velocityY;
        public bool flipX;
        public bool isDead;
        public int patrolState;
        public bool goingToB;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
    }

    void OnEnable()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player != null && player.IsInvulnerableBodyActive)
        {
            var playerCol = player.GetComponent<Collider2D>();
            var ownCol = GetComponent<Collider2D>();
            if (playerCol != null && ownCol != null)
                Physics2D.IgnoreCollision(playerCol, ownCol, true);
        }
    }

    void Start()
    {
        // Default points to current position if not set
        if (pointA == Vector2.zero && pointB == Vector2.zero)
        {
            pointA = (Vector2)transform.position;
            pointB = pointA + Vector2.right * 5f;
        }

        // Decide initial state
        float distToA = Vector2.Distance(transform.position, pointA);
        if (distToA < 0.05f)
        {
            state = PatrolState.PatrolAB;
            goingToB = true;
        }
        else
        {
            state = PatrolState.MovingToA;
        }
    }

    void Update()
    {
        if (isDead) return;

        switch (state)
        {
            case PatrolState.MovingToA:
                MoveTowards(pointA);
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), pointA) < 0.05f)
                {
                    transform.position = new Vector3(pointA.x, transform.position.y, transform.position.z);
                    state = PatrolState.PatrolAB;
                    goingToB = true;
                }
                break;

            case PatrolState.PatrolAB:
                Vector2 target = goingToB ? pointB : pointA;
                MoveTowards(target);
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.y), target) < 0.05f)
                {
                    transform.position = new Vector3(target.x, transform.position.y, transform.position.z);
                    goingToB = !goingToB;
                }
                break;
        }
    }

    private void MoveTowards(Vector2 target)
    {
        float dir = target.x > transform.position.x ? 1f : -1f;
        rb.velocity = new Vector2(dir * moveSpeed, rb.velocity.y);
        if (sr != null) sr.flipX = dir < 0f;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead || !col.gameObject.CompareTag("Player")) return;

        // 判断踩踏：接触点在敌人上方 + 玩家垂直速度向下
        bool stompedFromAbove = false;
        foreach (var contact in col.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                stompedFromAbove = true;
                break;
            }
        }

        var playerRb = col.gameObject.GetComponent<Rigidbody2D>();
        bool playerFalling = playerRb != null && playerRb.velocity.y < stompThreshold;

        if (stompedFromAbove && playerFalling)
        {
            // 玩家踩死敌人，给一个小弹跳
            playerRb.velocity = new Vector2(playerRb.velocity.x, 8f);
            Die();
        }
        else
        {
            // 侧面接触，玩家死亡
            col.gameObject.GetComponent<PlayerController>()?.Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.velocity = Vector2.zero;
        rb.simulated = false;
        GetComponent<Collider2D>().enabled = false;
        if (animator != null) animator.SetTrigger("Die");
        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        if (hideAfterDeathDelay > 0f)
            yield return new WaitForSeconds(hideAfterDeathDelay);

        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pointA, 0.15f);
        Gizmos.DrawSphere(pointB, 0.15f);
        Gizmos.DrawLine(pointA, pointB);

        // Show path from spawn to pointA
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, pointA);
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            activeSelf = gameObject.activeSelf && !isDead,
            positionX = transform.position.x,
            positionY = transform.position.y,
            positionZ = transform.position.z,
            velocityX = rb != null ? rb.velocity.x : 0f,
            velocityY = rb != null ? rb.velocity.y : 0f,
            flipX = sr != null && sr.flipX,
            isDead = isDead,
            patrolState = (int)state,
            goingToB = goingToB
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        transform.position = new Vector3(snapshot.positionX, snapshot.positionY, snapshot.positionZ);

        if (rb != null)
            rb.velocity = new Vector2(snapshot.velocityX, snapshot.velocityY);

        if (sr != null)
            sr.flipX = snapshot.flipX;

        isDead = snapshot.isDead;
        int maxEnum = (int)PatrolState.PatrolAB;
        state = (PatrolState)Mathf.Clamp(snapshot.patrolState, 0, maxEnum);
        goingToB = snapshot.goingToB;

        bool shouldBeActive = snapshot.activeSelf && !isDead;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = shouldBeActive;

        if (rb != null)
            rb.simulated = shouldBeActive;

        gameObject.SetActive(shouldBeActive);
    }
}
