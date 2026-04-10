using UnityEngine;

/// <summary>
/// 基础巡逻敌人。
/// - 先从初始位置移动到 pointA，然后在 pointA 和 pointB 之间来回巡逻
/// - 玩家从上方踩踏 → 敌人死亡
/// - 玩家侧面接触 → 玩家死亡
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour
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

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator animator;
    private bool isDead = false;

    // State machine
    private enum PatrolState { MovingToA, PatrolAB }
    private PatrolState state;
    private bool goingToB; // only used in PatrolAB state

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
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
        Destroy(gameObject, 0.5f);
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
}
