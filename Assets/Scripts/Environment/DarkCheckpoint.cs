using System;
using System.Collections;
using GestureRecognition.Core;
using GestureRecognition.Service;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DarkCheckpoint : MonoBehaviour, ISnapshotSaveable
{
    [Header("Dark Condition")]
    [SerializeField] private bool requireCameraOccluded = true;

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
    private bool playerInside;
    private bool cameraOccluded;
    private Coroutine activateCoroutine;

    [Serializable]
    private class SnapshotState
    {
        public bool activated;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }

    private void OnEnable()
    {
        GestureEvents.OnCameraOcclusionChanged += OnCameraOcclusionChanged;
    }

    private void OnDisable()
    {
        GestureEvents.OnCameraOcclusionChanged -= OnCameraOcclusionChanged;
        playerInside = false;
    }

    private void Start()
    {
        cameraOccluded = GestureService.Instance != null && GestureService.Instance.IsCameraOccluded;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activated && GameData.TryGetCheckpoint(activeScene.buildIndex, activeScene.path, out Vector3 checkpointPos))
        {
            if ((checkpointPos - transform.position).sqrMagnitude < 0.0001f)
                activated = true;
        }

        ApplyVisualState(activated);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        TryActivateDarkCheckpoint();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        TryActivateDarkCheckpoint();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = false;
    }

    private void OnCameraOcclusionChanged(bool occluded)
    {
        cameraOccluded = occluded;
        if (!playerInside)
            return;

        TryActivateDarkCheckpoint();
    }

    private void TryActivateDarkCheckpoint()
    {
        if (!MeetsDarkCondition())
            return;

        bool darkWasActive = GameData.IsDarkModeActive;
        if (!darkWasActive)
            GameData.ActivateDarkMode();

        if (activated)
        {
            if (!darkWasActive)
                SaveManager.SaveCheckpoint();
            return;
        }

        ActivateCheckpoint();
    }

    private bool MeetsDarkCondition()
    {
        if (!requireCameraOccluded)
            return true;

        if (cameraOccluded)
            return true;

        GestureService service = GestureService.Instance;
        return service != null && service.IsCameraOccluded;
    }

    private void ActivateCheckpoint()
    {
        activated = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCheckpoint(transform);
        }
        else
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameData.SetCheckpoint(activeScene.buildIndex, activeScene.path, transform.position);
        }

        SaveManager.SaveCheckpoint();

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
