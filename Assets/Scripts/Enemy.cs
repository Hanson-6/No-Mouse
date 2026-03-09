using UnityEngine;

/// <summary>
/// 基础巡逻敌人。
/// - 左右来回巡逻（遇墙/边缘自动转向）
/// - 玩家从上方踩踏 → 敌人死亡
/// - 玩家侧面接触 → 玩家死亡
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    [Header("Patrol")]
    public float moveSpeed = 2f;
    public float edgeCheckDistance = 0.5f;
    public LayerMask groundLayer;

    [Header("Stomp")]
    public float stompThreshold = 0.2f; // 玩家速度 y 低于此值才算踩踏

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator animator;
    private bool movingRight = true;
    private bool isDead = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (isDead) return;

        float dir = movingRight ? 1f : -1f;
        rb.velocity = new Vector2(dir * moveSpeed, rb.velocity.y);

        if (sr != null) sr.flipX = !movingRight;

        // 边缘检测
        Vector2 edgeCheck = transform.position + new Vector3(dir * 0.4f, -0.1f, 0);
        bool ground = Physics2D.Raycast(edgeCheck, Vector2.down, edgeCheckDistance, groundLayer);
        if (!ground) Flip();
    }

    void Flip() => movingRight = !movingRight;

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
        Destroy(gameObject, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        float dir = movingRight ? 1f : -1f;
        Vector2 edgeCheck = transform.position + new Vector3(dir * 0.4f, -0.6f, 0);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(edgeCheck, edgeCheck + Vector2.down * edgeCheckDistance);
    }
}
