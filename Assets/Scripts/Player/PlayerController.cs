using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Jump Settings")]
    public int maxJumpCount = 2;
    [SerializeField] private float jumpForce = 14f;
    // Multiplies gravityScale while the player is falling — higher = snappier landing
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    // Multiplies gravityScale while rising with jump button released — controls short-hop height
    [SerializeField] private float lowJumpMultiplier = 2f;
    // Fraction of upward velocity kept when jump button is released early (0=instant cut, 1=no cut)
    [SerializeField] private float jumpCutMultiplier = 0.45f;

    [Header("Audio")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip doubleJumpSound;
    [SerializeField] private AudioClip deathSound;

    private AudioSource audioSource;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;

    private float defaultGravityScale;
    private float moveInput;
    private float autoWalkDirection = 0f;
    private bool isGrounded;
    /// <summary>玩家是否在地面上。GestureInputBridge 用于判断跳跃释放抓取。</summary>
    public bool IsGrounded => isGrounded;
    private int jumpCount;
    private bool isDead;
    // ── 手势系统控制字段 ──────────────────────────────────────────────────
    // facingLocked: Pull 时锁定面朝方向，防止 sprite 翻转
    // moveDirection: 0=不限制, 1=只能往右, -1=只能往左
    //   Push 时设为面朝方向（只能推着走）
    //   Pull 时设为面朝反方向（只能拉着走）
    [HideInInspector] public bool facingLocked;
    [HideInInspector] public int moveDirection;

    /// <summary>玩家当前是否面朝右。GestureInputBridge 用来判断面朝 Box 条件。</summary>
    public bool FacingRight => !spriteRenderer.flipX;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        defaultGravityScale = rb.gravityScale;
    }

    void Update()
    {
        if (isDead) return;

        moveInput = Input.GetAxisRaw("Horizontal");

        // 方向限制：Push 时只能往面朝方向走，Pull 时只能往反方向走
        if (moveDirection != 0 && moveInput != 0f)
        {
            if (Mathf.Sign(moveInput) != Mathf.Sign(moveDirection))
                moveInput = 0f;
        }

        // Jump
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumpCount)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (audioSource != null)
            {
                AudioClip clip = (jumpCount == 0) ? jumpSound : (doubleJumpSound != null ? doubleJumpSound : jumpSound);
                if (clip != null) audioSource.PlayOneShot(clip);
            }
            jumpCount++;
        }

        // Jump cut — bleed upward velocity when button is released early
        if (Input.GetButtonUp("Jump") && rb.velocity.y > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
        }

        // 面朝方向翻转（Pull 时锁定，不允许翻转）
        if (!facingLocked)
        {
            if (moveInput > 0) spriteRenderer.flipX = false;
            else if (moveInput < 0) spriteRenderer.flipX = true;
        }

        animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VelocityY", rb.velocity.y);
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            if (autoWalkDirection != 0f)
                rb.velocity = new Vector2(autoWalkDirection * moveSpeed, rb.velocity.y);
            return;
        }

        // Ground check — thin box so walls don't falsely trigger grounded state
        isGrounded = Physics2D.OverlapBox(groundCheck.position, new Vector2(0.15f, 0.05f), 0f, groundLayer);
        if (!isGrounded)
        {
            // 站在箱子上也算落地：RaycastAll 过滤掉自身碰撞体
            var hits = Physics2D.RaycastAll(groundCheck.position, Vector2.down, 0.2f);
            foreach (var h in hits)
            {
                if (h.collider == ownCollider) continue;
                if (h.collider.CompareTag("Box")) { isGrounded = true; break; }
            }
        }
        if (isGrounded) jumpCount = 0;

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        // Gravity modulation — heavier fall, snappier short-hop
        if (rb.velocity.y < 0f)
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        else if (rb.velocity.y > 0f && !Input.GetButton("Jump"))
            rb.gravityScale = defaultGravityScale * lowJumpMultiplier;
        else
            rb.gravityScale = defaultGravityScale;
    }

    public void AutoWalk(float direction)
    {
        isDead = true;
        autoWalkDirection = direction;
        rb.gravityScale = defaultGravityScale;
        spriteRenderer.flipX = direction < 0f;
        animator.SetFloat("Speed", 1f);
    }

    public void LockInput()
    {
        isDead = true;
        rb.velocity = Vector2.zero;
        rb.gravityScale = defaultGravityScale;
        animator.SetFloat("Speed", 0f);
    }

    public void Die()
    {
        if (isDead) return; // 守卫：防止 Die() 被重复调用
        isDead = true; // 标记死亡状态，阻止后续 Update/FixedUpdate 逻辑

        rb.velocity = Vector2.zero; // Unity 内置 --> 停止玩家移动
        animator.SetTrigger("Die"); // Unity 内置 --> 播放死亡动画
        if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
        Invoke(nameof(Respawn), 1.5f); // Unity 内置 --> 1.5 秒后调用 Respawn()，重置关卡
    }

    void Respawn()
    {
        // 调用 GameManager 单例的重新加载关卡方法
        GameManager.Instance.RestartLevel();
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, new Vector3(0.15f, 0.05f, 0f));
        }
    }
}
