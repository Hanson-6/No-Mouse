using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FallingBoulder : MonoBehaviour, ISnapshotSaveable
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float gravityScale = 5f;
    [SerializeField, Min(0.01f)] private float killMinDownwardSpeed = 0.2f;

    [Header("Visuals")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] fallingFrames;
    [SerializeField, Min(1f)] private float fallingFps = 10f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;

    private float frameTimer;
    private int frameIndex;
    private bool wasMovingDownward;

    [System.Serializable]
    private class SnapshotState
    {
        public bool activeSelf;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float velocityX;
        public float velocityY;
        public bool simulated;
        public int constraints;
        public int frameIndex;
        public float frameTimer;
        public bool wasMovingDownward;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        boxCollider.isTrigger = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravityScale;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnValidate()
    {
        gravityScale = Mathf.Max(0f, gravityScale);
        killMinDownwardSpeed = Mathf.Max(0.01f, killMinDownwardSpeed);
        fallingFps = Mathf.Max(1f, fallingFps);

        if (!Application.isPlaying)
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider != null)
                collider.isTrigger = false;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = gravityScale;
                body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            }
        }
    }

    void Update()
    {
        bool isMovingDownward = IsMovingDownward();
        UpdateVisual(isMovingDownward);
        wasMovingDownward = isMovingDownward;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryKillPlayer(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryKillPlayer(collision.collider);
    }

    private void TryKillPlayer(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;

        if (!IsMovingDownward())
            return;

        other.GetComponent<PlayerController>()?.Die();
    }

    private bool IsMovingDownward()
    {
        return rb != null && rb.simulated && rb.velocity.y < -killMinDownwardSpeed;
    }

    private void UpdateVisual(bool isMovingDownward)
    {
        if (spriteRenderer == null)
            return;

        if (!isMovingDownward)
        {
            frameTimer = 0f;
            frameIndex = 0;

            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
            else if (fallingFrames != null && fallingFrames.Length > 0 && fallingFrames[0] != null)
                spriteRenderer.sprite = fallingFrames[0];

            return;
        }

        if (fallingFrames == null || fallingFrames.Length == 0)
            return;

        if (fallingFrames.Length == 1)
        {
            if (fallingFrames[0] != null)
                spriteRenderer.sprite = fallingFrames[0];
            return;
        }

        float frameDuration = 1f / fallingFps;
        frameTimer += Time.deltaTime;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % fallingFrames.Length;
        }

        Sprite frame = fallingFrames[frameIndex];
        if (frame != null)
            spriteRenderer.sprite = frame;
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
            simulated = rb != null && rb.simulated,
            constraints = rb != null ? (int)rb.constraints : 0,
            frameIndex = frameIndex,
            frameTimer = frameTimer,
            wasMovingDownward = wasMovingDownward
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        gameObject.SetActive(snapshot.activeSelf);
        transform.position = new Vector3(snapshot.positionX, snapshot.positionY, snapshot.positionZ);

        if (rb != null)
        {
            rb.velocity = new Vector2(snapshot.velocityX, snapshot.velocityY);
            rb.simulated = snapshot.simulated;
            rb.constraints = (RigidbodyConstraints2D)snapshot.constraints;
            rb.gravityScale = gravityScale;
        }

        frameIndex = Mathf.Max(0, snapshot.frameIndex);
        frameTimer = Mathf.Max(0f, snapshot.frameTimer);
        wasMovingDownward = snapshot.wasMovingDownward;

        UpdateVisual(IsMovingDownward());
    }
}
