using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, ISnapshotSaveable
{
    [Header("Movement")]
    public float moveSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    [SerializeField] private float groundCheckWidth = 0.75f;
    [SerializeField, Range(0f, 1f)] private float groundedNormalThreshold = 0.55f;
    public LayerMask groundLayer;

    [Header("Jump Settings")]
    public int maxJumpCount = 2;
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float coyoteTime = 0.12f;
    // Multiplies gravityScale while the player is falling — higher = snappier landing
    [SerializeField] private float fallGravityMultiplier = 3.5f;

    [Header("Step Climb")]
    [Tooltip("Max height the player can automatically step up (e.g. small ledge between platform and terrain)")]
    [SerializeField] private float maxStepHeight = 0.465f;
    [Tooltip("Horizontal distance to probe ahead for steps")]
    [SerializeField] private float stepCheckDistance = 0.15f;
    [Tooltip("Edge radius on the BoxCollider2D — rounds corners to slide over tiny height differences")]
    [SerializeField] private float colliderEdgeRadius = 0.02f;

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
    private float jumpBufferTimer;
    private float coyoteTimer;
    private bool isDead;
    private readonly int doubleJumpTriggerHash = Animator.StringToHash("DoubleJump");
    private bool hasDoubleJumpTrigger;
    private readonly RaycastHit2D[] groundRayHits = new RaycastHit2D[8];
    private readonly Collider2D[] groundOverlapHits = new Collider2D[8];
    private ContactFilter2D groundProbeFilter;
    private MovingPlatform currentPlatform;
    // ── 手势系统控制字段 ──────────────────────────────────────────────────
    // facingLocked: Pull 时锁定面朝方向，防止 sprite 翻转
    // moveDirection: 0=不限制, 1=只能往右, -1=只能往左
    //   Push 时设为面朝方向（只能推着走）
    //   Pull 时设为面朝反方向（只能拉着走）
    [HideInInspector] public bool facingLocked;
    [HideInInspector] public int moveDirection;

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
        public bool facingLocked;
        public int moveDirection;
        public bool isDead;
        public float autoWalkDirection;
        public int jumpCount;
        public float jumpBufferTimer;
        public float coyoteTimer;
    }

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
        hasDoubleJumpTrigger = HasAnimatorParameter(doubleJumpTriggerHash, AnimatorControllerParameterType.Trigger);

        groundProbeFilter = new ContactFilter2D();
        groundProbeFilter.SetLayerMask(Physics2D.AllLayers);
        groundProbeFilter.useTriggers = false;

        // Round the bottom corners of the BoxCollider2D so the player
        // can slide over tiny height differences (< edgeRadius) naturally.
        var box = ownCollider as BoxCollider2D;
        if (box != null && colliderEdgeRadius > 0f)
        {
            box.edgeRadius = colliderEdgeRadius;
        }
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
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
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

        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            jumpCount = 0;
            coyoteTimer = coyoteTime;
        }
        else if (coyoteTimer > 0f)
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        TryConsumeBufferedJump();

        float horizontalVelocity = moveInput * moveSpeed;
        if (isGrounded && currentPlatform != null && Mathf.Abs(moveInput) > 0.01f)
            horizontalVelocity -= currentPlatform.CurrentVelocityX;

        rb.velocity = new Vector2(horizontalVelocity, rb.velocity.y);

        // Step-up: auto-climb small ledges when grounded and moving
        if (isGrounded && Mathf.Abs(moveInput) > 0.01f)
            TryStepUp(moveInput > 0f ? Vector2.right : Vector2.left);

        // Gravity modulation — heavier fall only
        if (rb.velocity.y < 0f)
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        else
            rb.gravityScale = defaultGravityScale;
    }

    void TryConsumeBufferedJump()
    {
        if (jumpBufferTimer <= 0f) return;

        bool canGroundJump = (isGrounded || coyoteTimer > 0f) && jumpCount == 0;
        bool canAirJump = !canGroundJump && jumpCount < maxJumpCount;
        if (!canGroundJump && !canAirJump) return;

        bool isAirJump = !canGroundJump;
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);

        if (audioSource != null)
        {
            AudioClip clip = (jumpCount == 0) ? jumpSound : (doubleJumpSound != null ? doubleJumpSound : jumpSound);
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        if (isAirJump && hasDoubleJumpTrigger)
            animator.SetTrigger(doubleJumpTriggerHash);

        jumpCount++;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    void TryStepUp(Vector2 direction)
    {
        if (ownCollider == null || maxStepHeight <= 0f) return;

        Bounds bounds = ownCollider.bounds;

        float halfWidth = bounds.extents.x;
        float rayOriginX = bounds.center.x;
        float totalRayDist = halfWidth + stepCheckDistance;

        Vector2 footOrigin = new Vector2(rayOriginX, bounds.min.y + 0.02f);

        // 1. Foot-level ray — is there a ledge ahead?
        RaycastHit2D lowHit = Physics2D.Raycast(footOrigin, direction, totalRayDist, groundLayer);
        if (lowHit.collider == null) return;

        float wallX = lowHit.point.x;
        float leadingX = direction.x > 0f ? bounds.max.x : bounds.min.x;

        // 2. High ray — if clear, it's a step, not a wall
        Vector2 highOrigin = new Vector2(rayOriginX, bounds.min.y + maxStepHeight);
        RaycastHit2D highHit = Physics2D.Raycast(highOrigin, direction, totalRayDist, groundLayer);
        if (highHit.collider != null) return;

        // 3. Downward ray to find step surface
        float distToWall = Mathf.Abs(wallX - leadingX);
        float probePastWall = distToWall + 0.08f;
        Vector2 overStepOrigin = new Vector2(
            leadingX + direction.x * probePastWall,
            bounds.min.y + maxStepHeight + 0.02f);
        RaycastHit2D downHit = Physics2D.Raycast(overStepOrigin, Vector2.down, maxStepHeight + 0.04f, groundLayer);
        if (downHit.collider == null) return;

        float heightDiff = downHit.point.y - bounds.min.y;
        if (heightDiff <= 0.005f || heightDiff > maxStepHeight) return;

        // 4. Teleport UP + FORWARD
        float forwardNudge = probePastWall + 0.02f;
        Vector2 newPos = rb.position;
        newPos.y += heightDiff + 0.02f;
        newPos.x += direction.x * forwardNudge;
        rb.position = newPos;

        if (rb.velocity.y < 0f)
            rb.velocity = new Vector2(rb.velocity.x, 0f);
    }

    bool CheckGrounded()
    {
        if (ownCollider == null || groundCheck == null) return false;

        Vector2 probeCenter = groundCheck.position;
        Vector2 probeSize = new Vector2(Mathf.Max(groundCheckWidth, 0.2f), Mathf.Max(groundCheckRadius, 0.1f));

        int hitCount = Physics2D.OverlapBox(probeCenter, probeSize, 0f, groundProbeFilter, groundOverlapHits);
        bool touchingGroundCandidate = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundOverlapHits[i];
            if (hit == null || hit == ownCollider) continue;

            bool onGroundLayer = ((1 << hit.gameObject.layer) & groundLayer.value) != 0;
            if (onGroundLayer || hit.CompareTag("Box"))
            {
                touchingGroundCandidate = true;
                break;
            }
        }

        if (!touchingGroundCandidate) return false;

        float halfWidth = probeSize.x * 0.45f;
        Vector2 originBase = probeCenter + Vector2.up * 0.03f;
        float rayDistance = Mathf.Max(groundCheckRadius + 0.12f, 0.18f);

        return HasGroundSupport(originBase, rayDistance)
            || HasGroundSupport(originBase + Vector2.left * halfWidth, rayDistance)
            || HasGroundSupport(originBase + Vector2.right * halfWidth, rayDistance);
    }

    bool HasGroundSupport(Vector2 origin, float distance)
    {
        int hitCount = Physics2D.Raycast(origin, Vector2.down, groundProbeFilter, groundRayHits, distance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundRayHits[i];
            if (hit.collider == null || hit.collider == ownCollider) continue;
            if (hit.normal.y < groundedNormalThreshold) continue;

            bool onGroundLayer = ((1 << hit.collider.gameObject.layer) & groundLayer.value) != 0;
            if (onGroundLayer || hit.collider.CompareTag("Box"))
                return true;
        }

        return false;
    }

    bool HasAnimatorParameter(int paramHash, AnimatorControllerParameterType expectedType)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == paramHash && parameter.type == expectedType)
                return true;
        }

        return false;
    }

    public void SetCurrentPlatform(MovingPlatform platform)
    {
        currentPlatform = platform;
    }

    public void ClearCurrentPlatform(MovingPlatform platform)
    {
        if (currentPlatform == platform)
            currentPlatform = null;
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

            Vector3 center = groundCheck.position;
            float halfWidth = groundCheckWidth * 0.5f;

            Gizmos.DrawLine(center, center + Vector3.down * groundCheckRadius);
            Gizmos.DrawLine(center + Vector3.left * halfWidth, center + Vector3.left * halfWidth + Vector3.down * groundCheckRadius);
            Gizmos.DrawLine(center + Vector3.right * halfWidth, center + Vector3.right * halfWidth + Vector3.down * groundCheckRadius);
        }
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            activeSelf = gameObject.activeSelf,
            positionX = transform.position.x,
            positionY = transform.position.y,
            positionZ = transform.position.z,
            velocityX = rb != null ? rb.velocity.x : 0f,
            velocityY = rb != null ? rb.velocity.y : 0f,
            flipX = spriteRenderer != null && spriteRenderer.flipX,
            facingLocked = facingLocked,
            moveDirection = moveDirection,
            isDead = isDead,
            autoWalkDirection = autoWalkDirection,
            jumpCount = jumpCount,
            jumpBufferTimer = jumpBufferTimer,
            coyoteTimer = coyoteTimer
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        gameObject.SetActive(snapshot.activeSelf);

        transform.position = new Vector3(snapshot.positionX, snapshot.positionY, snapshot.positionZ);

        if (rb != null)
        {
            rb.velocity = new Vector2(snapshot.velocityX, snapshot.velocityY);
            rb.gravityScale = defaultGravityScale;
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = snapshot.flipX;

        facingLocked = snapshot.facingLocked;
        moveDirection = snapshot.moveDirection;
        isDead = snapshot.isDead;
        autoWalkDirection = snapshot.autoWalkDirection;
        jumpCount = snapshot.jumpCount;
        jumpBufferTimer = snapshot.jumpBufferTimer;
        coyoteTimer = snapshot.coyoteTimer;

        currentPlatform = null;
    }
}
