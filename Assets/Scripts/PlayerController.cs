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

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float defaultGravityScale;
    private float moveInput;
    private bool isGrounded;
    private int jumpCount;
    private bool isDead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultGravityScale = rb.gravityScale;
    }

    void Update()
    {
        if (isDead) return;

        moveInput = Input.GetAxisRaw("Horizontal");

        // Jump
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumpCount)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpCount++;
        }

        // Jump cut — bleed upward velocity when button is released early
        if (Input.GetButtonUp("Jump") && rb.velocity.y > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutMultiplier);
        }

        // Sprite flip
        if (moveInput > 0) spriteRenderer.flipX = false;
        else if (moveInput < 0) spriteRenderer.flipX = true;

        // Animations
        animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VelocityY", rb.velocity.y);
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // Ground check — thin box so walls don't falsely trigger grounded state
        isGrounded = Physics2D.OverlapBox(groundCheck.position, new Vector2(0.15f, 0.05f), 0f, groundLayer);
        if (isGrounded) jumpCount = 0;

        // Move
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        // Gravity modulation — heavier fall, snappier short-hop
        if (rb.velocity.y < 0f)
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        else if (rb.velocity.y > 0f && !Input.GetButton("Jump"))
            rb.gravityScale = defaultGravityScale * lowJumpMultiplier;
        else
            rb.gravityScale = defaultGravityScale;
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
        if (isDead) return;
        isDead = true;
        rb.velocity = Vector2.zero;
        animator.SetTrigger("Die");
        Invoke(nameof(Respawn), 1.5f);
    }

    void Respawn()
    {
        transform.position = GameManager.Instance.GetRespawnPoint();
        rb.gravityScale = defaultGravityScale;
        isDead = false;
        animator.ResetTrigger("Die");
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
