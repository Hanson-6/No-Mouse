using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Checkpoint : MonoBehaviour, ISnapshotSaveable
{
    [Header("Visual")]
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeLoopSprite;
    [SerializeField] private Sprite[] activateFrames;
    [SerializeField] private float activateFps = 12f;

    [Header("Audio")]
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private float activateSoundVolume = 1f;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private bool activated;
    private Coroutine activateCoroutine;

    [Serializable]
    private class SnapshotState
    {
        public bool activated;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        var trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }

    void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activated && GameData.TryGetCheckpoint(activeScene.buildIndex, activeScene.path, out Vector3 checkpointPos))
        {
            if ((checkpointPos - transform.position).sqrMagnitude < 0.0001f)
                activated = true;
        }

        ApplyVisualState(activated);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        ActivateCheckpoint();
    }

    private void ActivateCheckpoint()
    {
        activated = true;

        if (GameManager.Instance != null)
            GameManager.Instance.SetCheckpoint(transform);
        else
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameData.SetCheckpoint(activeScene.buildIndex, activeScene.path, transform.position);
        }

        if (audioSource != null && activateSound != null)
            audioSource.PlayOneShot(activateSound, activateSoundVolume);

        if (activateCoroutine != null)
            StopCoroutine(activateCoroutine);

        activateCoroutine = StartCoroutine(PlayActivationAnimation());
    }

    private IEnumerator PlayActivationAnimation()
    {
        if (activateFrames != null && activateFrames.Length > 0)
        {
            float frameDuration = activateFps > 0f ? 1f / activateFps : 0.08f;
            for (int i = 0; i < activateFrames.Length; i++)
            {
                if (activateFrames[i] != null)
                    spriteRenderer.sprite = activateFrames[i];

                yield return new WaitForSeconds(frameDuration);
            }
        }

        SetActiveVisual();
        activateCoroutine = null;
    }

    private void ApplyVisualState(bool isActive)
    {
        if (!isActive)
        {
            if (inactiveSprite != null)
                spriteRenderer.sprite = inactiveSprite;
            return;
        }

        SetActiveVisual();
    }

    private void SetActiveVisual()
    {
        if (activeLoopSprite != null)
        {
            spriteRenderer.sprite = activeLoopSprite;
            return;
        }

        if (activateFrames == null || activateFrames.Length == 0)
            return;

        for (int i = activateFrames.Length - 1; i >= 0; i--)
        {
            if (activateFrames[i] != null)
            {
                spriteRenderer.sprite = activateFrames[i];
                return;
            }
        }
    }

    public string CaptureSnapshotState()
    {
        return JsonUtility.ToJson(new SnapshotState
        {
            activated = activated
        });
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState state = JsonUtility.FromJson<SnapshotState>(stateJson);
        activated = state != null && state.activated;

        if (activateCoroutine != null)
        {
            StopCoroutine(activateCoroutine);
            activateCoroutine = null;
        }

        ApplyVisualState(activated);
    }
}
