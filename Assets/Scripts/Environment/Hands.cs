using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Hands : MonoBehaviour, ISnapshotSaveable
{
    [Header("On Consume")]
    [SerializeField] private GameObject passageTarget;

    [Header("Visual")]
    [SerializeField] private Sprite inactiveSprite;

    [Header("Audio")]
    [SerializeField] private AudioClip consumeSound;
    [SerializeField] private float consumeSoundVolume = 1f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D triggerCollider;
    private AudioSource audioSource;
    private bool consumed;

    [Serializable]
    private class SnapshotState
    {
        public bool consumed;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<BoxCollider2D>();
        audioSource = GetComponent<AudioSource>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Start()
    {
        ApplyConsumedState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        TryConsume();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        TryConsume();
    }

    private void TryConsume()
    {
        if (consumed)
            return;

        consumed = true;

        ActivatePassageTarget();

        if (audioSource != null && consumeSound != null)
            audioSource.PlayOneShot(consumeSound, consumeSoundVolume);

        ApplyConsumedState();
    }

    private void ActivatePassageTarget()
    {
        if (passageTarget == null)
            return;

        bool activatedAny = false;
        MonoBehaviour[] behaviours = passageTarget.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IButtonActivatable activatable)
            {
                activatable.Activate();
                activatedAny = true;
            }
        }

        if (!activatedAny && !passageTarget.activeSelf)
            passageTarget.SetActive(true);
    }

    private void ApplyConsumedState()
    {
        if (triggerCollider != null)
            triggerCollider.enabled = !consumed;

        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = !consumed;

        if (!consumed && inactiveSprite != null)
            spriteRenderer.sprite = inactiveSprite;
    }

    public string CaptureSnapshotState()
    {
        return JsonUtility.ToJson(new SnapshotState
        {
            consumed = consumed
        });
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState state = JsonUtility.FromJson<SnapshotState>(stateJson);
        consumed = state != null && state.consumed;
        ApplyConsumedState();
    }
}
