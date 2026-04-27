using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class FireTrap : MonoBehaviour
{
    private enum Phase
    {
        Off,
        Warning,
        Hit
    }

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float burstInterval = 2f;
    [SerializeField, Min(0f)] private float warningDuration = 0.2f;
    [SerializeField, Min(0f)] private float hitDuration = 0.5f;

    [Header("Visuals")]
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite[] onFrames;
    [SerializeField, Min(1f)] private float onFps = 12f;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField, Min(1f)] private float hitFps = 12f;

    [Header("Audio")]
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Min(0f)] private float warningSoundVolume = 1f;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Min(0f)] private float hitSoundVolume = 1f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D triggerCollider;
    private AudioSource audioSource;
    private Phase phase;
    private float phaseTimer;
    private float frameTimer;
    private int frameIndex;

    private float OffDuration => Mathf.Max(0f, burstInterval - warningDuration - hitDuration);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<BoxCollider2D>();
        audioSource = GetComponent<AudioSource>();
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = false;
    }

    void OnValidate()
    {
        burstInterval = Mathf.Max(0.1f, burstInterval);
        warningDuration = Mathf.Max(0f, warningDuration);
        hitDuration = Mathf.Max(0f, hitDuration);
        onFps = Mathf.Max(1f, onFps);
        hitFps = Mathf.Max(1f, hitFps);
        warningSoundVolume = Mathf.Max(0f, warningSoundVolume);
        hitSoundVolume = Mathf.Max(0f, hitSoundVolume);

        if (!Application.isPlaying)
        {
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
                col.isTrigger = true;
        }
    }

    void Start()
    {
        EnterPhase(Phase.Off);
    }

    void Update()
    {
        phaseTimer += Time.deltaTime;
        AdvancePhaseIfNeeded();
        AnimateCurrentPhase();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryKillPlayer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryKillPlayer(other);
    }

    private void TryKillPlayer(Collider2D other)
    {
        if (phase != Phase.Hit)
            return;

        if (!other.CompareTag("Player"))
            return;

        other.GetComponent<PlayerController>()?.Die();
    }

    private void AdvancePhaseIfNeeded()
    {
        for (int i = 0; i < 3; i++)
        {
            if (phase == Phase.Off && phaseTimer >= OffDuration)
            {
                EnterPhase(Phase.Warning);
                continue;
            }

            if (phase == Phase.Warning && phaseTimer >= warningDuration)
            {
                EnterPhase(Phase.Hit);
                continue;
            }

            if (phase == Phase.Hit && phaseTimer >= hitDuration)
            {
                EnterPhase(Phase.Off);
                continue;
            }

            break;
        }
    }

    private void EnterPhase(Phase nextPhase)
    {
        phase = nextPhase;
        phaseTimer = 0f;
        frameTimer = 0f;
        frameIndex = 0;
        SetHitColliderActive(nextPhase == Phase.Hit);
        ApplyPhaseFirstFrame();
        PlayPhaseSound(nextPhase);
    }

    private void SetHitColliderActive(bool active)
    {
        if (triggerCollider == null)
            return;

        if (triggerCollider.enabled != active)
            triggerCollider.enabled = active;
    }

    private void PlayPhaseSound(Phase enteredPhase)
    {
        if (audioSource == null)
            return;

        switch (enteredPhase)
        {
            case Phase.Off:
                if (audioSource.isPlaying)
                    audioSource.Stop();
                break;
            case Phase.Warning:
                if (warningSound != null)
                    audioSource.PlayOneShot(warningSound, warningSoundVolume);
                break;
            case Phase.Hit:
                if (hitSound != null)
                    audioSource.PlayOneShot(hitSound, hitSoundVolume);
                break;
        }
    }

    private void ApplyPhaseFirstFrame()
    {
        switch (phase)
        {
            case Phase.Off:
                if (offSprite != null)
                    spriteRenderer.sprite = offSprite;
                break;
            case Phase.Warning:
                SetFrame(onFrames, 0);
                break;
            case Phase.Hit:
                SetFrame(hitFrames, 0);
                break;
        }
    }

    private void AnimateCurrentPhase()
    {
        switch (phase)
        {
            case Phase.Off:
                if (offSprite != null && spriteRenderer.sprite != offSprite)
                    spriteRenderer.sprite = offSprite;
                break;
            case Phase.Warning:
                AdvanceAnimation(onFrames, onFps);
                break;
            case Phase.Hit:
                AdvanceAnimation(hitFrames, hitFps);
                break;
        }
    }

    private void AdvanceAnimation(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
            return;

        if (frames.Length == 1)
        {
            SetFrame(frames, 0);
            return;
        }

        float frameDuration = 1f / fps;
        frameTimer += Time.deltaTime;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        SetFrame(frames, frameIndex);
    }

    private void SetFrame(Sprite[] frames, int index)
    {
        if (frames == null || frames.Length == 0)
            return;

        int safeIndex = Mathf.Clamp(index, 0, frames.Length - 1);
        Sprite sprite = frames[safeIndex];
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }
}
