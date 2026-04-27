using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DisappearPlatform : MonoBehaviour, ISnapshotSaveable
{
    private enum PlatformState
    {
        Idle,
        Priming,
        Disappeared
    }

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float disappearDelay = 1f;

    [Header("Visuals")]
    [SerializeField] private Sprite idleSprite;
    [FormerlySerializedAs("glowFrames")]
    [SerializeField] private Sprite[] breakingFrames;
    [SerializeField] private bool hideRendererOnDisappear = true;

    [Header("Detection")]
    [SerializeField, Range(0f, 0.2f)] private float topContactTolerance = 0.06f;

    private SpriteRenderer spriteRenderer;
    private Collider2D platformCollider;
    private PlatformState state;
    private float primeTimer;

    [System.Serializable]
    private class SnapshotState
    {
        public int state;
        public float primeTimer;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        platformCollider = GetComponent<Collider2D>();
        if (platformCollider != null)
            platformCollider.isTrigger = false;
    }

    void OnValidate()
    {
        disappearDelay = Mathf.Max(0.01f, disappearDelay);
        topContactTolerance = Mathf.Clamp(topContactTolerance, 0f, 0.2f);

        if (!Application.isPlaying)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = false;
        }
    }

    void Start()
    {
        EnterIdleState();
    }

    void Update()
    {
        if (state != PlatformState.Priming)
            return;

        primeTimer += Time.deltaTime;
        ApplyPrimingVisual();

        if (primeTimer >= disappearDelay)
            EnterDisappearedState();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartPriming(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryStartPriming(collision);
    }

    private void TryStartPriming(Collision2D collision)
    {
        if (state != PlatformState.Idle)
            return;

        Collider2D other = collision.collider;
        if (other == null || !other.CompareTag("Player"))
            return;

        if (!IsPlayerStandingOnTop(other))
            return;

        EnterPrimingState();
    }

    private bool IsPlayerStandingOnTop(Collider2D playerCollider)
    {
        if (platformCollider == null || playerCollider == null)
            return false;

        Bounds platformBounds = platformCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;

        if (playerBounds.max.x < platformBounds.min.x || playerBounds.min.x > platformBounds.max.x)
            return false;

        float playerBottom = playerBounds.min.y;
        float platformTop = platformBounds.max.y;
        return playerBottom >= platformTop - topContactTolerance;
    }

    private void EnterIdleState()
    {
        state = PlatformState.Idle;
        primeTimer = 0f;

        if (platformCollider != null)
            platformCollider.enabled = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            SetSprite(idleSprite);
        }
    }

    private void EnterPrimingState()
    {
        state = PlatformState.Priming;
        primeTimer = 0f;

        if (platformCollider != null)
            platformCollider.enabled = true;

        ApplyPrimingVisual();
    }

    private void EnterDisappearedState()
    {
        state = PlatformState.Disappeared;
        primeTimer = disappearDelay;

        if (platformCollider != null)
            platformCollider.enabled = false;

        if (spriteRenderer != null)
        {
            if (hideRendererOnDisappear)
            {
                spriteRenderer.enabled = false;
            }
            else
            {
                spriteRenderer.enabled = true;
                SetSprite(GetLastBreakingFrameOrIdle());
            }
        }
    }

    private void ApplyPrimingVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = true;

        if (breakingFrames == null || breakingFrames.Length == 0)
        {
            SetSprite(idleSprite);
            return;
        }

        float normalized = disappearDelay <= 0f ? 1f : Mathf.Clamp01(primeTimer / disappearDelay);
        int frameIndex = Mathf.Min(breakingFrames.Length - 1, Mathf.FloorToInt(normalized * breakingFrames.Length));
        SetSprite(breakingFrames[frameIndex]);
    }

    private Sprite GetLastBreakingFrameOrIdle()
    {
        if (breakingFrames != null && breakingFrames.Length > 0)
            return breakingFrames[breakingFrames.Length - 1];

        return idleSprite;
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer == null || sprite == null)
            return;

        spriteRenderer.sprite = sprite;
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            state = (int)state,
            primeTimer = primeTimer
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        int maxState = (int)PlatformState.Disappeared;
        int clampedState = Mathf.Clamp(snapshot.state, 0, maxState);

        state = (PlatformState)clampedState;
        primeTimer = Mathf.Clamp(snapshot.primeTimer, 0f, disappearDelay);

        ApplyStateAfterRestore();
    }

    private void ApplyStateAfterRestore()
    {
        if (state == PlatformState.Idle)
        {
            EnterIdleState();
            return;
        }

        if (state == PlatformState.Priming)
        {
            if (platformCollider != null)
                platformCollider.enabled = true;

            ApplyPrimingVisual();
            return;
        }

        EnterDisappearedState();
    }
}
