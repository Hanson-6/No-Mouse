using System;
using System.Collections;
using GestureRecognition.Core;
using GestureRecognition.Service;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DarkCheckpoint : MonoBehaviour, ISnapshotSaveable
{
    private const string SharedHintCanvasName = "HintCanvas";
    private const string RuntimeHintCanvasName = "DarkCheckpointHintCanvas";
    private const string HintPanelName = "HintPanel";

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

    [Header("Hint Panel")]
    [SerializeField] private bool showHintOnFirstActivation = true;
    [SerializeField] private Sprite hintSprite;
    [SerializeField, TextArea(2, 6)] private string tipText = "Cover your camera to trigger dark mode.";
    [SerializeField, Min(0f)] private float openDuration = 0.25f;
    [SerializeField, Min(0f)] private float closeDuration = 0.15f;

    public static bool IsAnyHintPanelOpen => openPanelCount > 0;
    public static bool DidConsumeEscapeThisFrame => lastEscapeCloseFrame == Time.frameCount;

    private static int openPanelCount;
    private static int lastEscapeCloseFrame = -1;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private bool activated;
    private bool playerInside;
    private bool cameraOccluded;
    private bool activationAnimationCompleted;
    private bool pendingHintAfterAnimation;
    private bool requireHintCloseBeforeDarkMode;
    private bool hasShownHintOnce;
    private bool isHintPanelOpen;
    private Coroutine activateCoroutine;
    private Coroutine hintAnimCoroutine;

    private GameObject hintPanel;
    private RectTransform hintPanelRect;
    private Image hintImage;
    private Text tipLabel;
    private Rigidbody2D playerRb;
    private RigidbodyConstraints2D originalPlayerConstraints;
    private bool hasFrozenPlayerForHint;

    [Serializable]
    private class SnapshotState
    {
        public bool activated;
        public bool hasShownHintOnce;
    }

    private void OnValidate()
    {
        activateFps = Mathf.Max(1f, activateFps);
        activateSoundVolume = Mathf.Max(0f, activateSoundVolume);
        openDuration = Mathf.Max(0f, openDuration);
        closeDuration = Mathf.Max(0f, closeDuration);
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
        ForceHideHintPanel();
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

        if (activated)
        {
            hasShownHintOnce = true;
            activationAnimationCompleted = true;
        }

        ApplyVisualState(activated);
    }

    private void Update()
    {
        if (isHintPanelOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseHintPanel(closedByEscape: true);

        if (!playerInside)
            return;

        TryActivateDarkModeIfReady();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandlePlayerContact(other);
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

        TryActivateDarkModeIfReady();
    }

    private void HandlePlayerContact(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        if (playerRb == null)
            playerRb = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody2D>();

        ActivateCheckpointIfNeeded();
        TryActivateDarkModeIfReady();
    }

    private void ActivateCheckpointIfNeeded()
    {
        if (activated)
            return;

        ActivateCheckpoint();
    }

    private void TryShowHintOnFirstActivation()
    {
        pendingHintAfterAnimation = false;

        if (!EnsureHintPanelBound())
        {
            requireHintCloseBeforeDarkMode = false;
            return;
        }

        if (hintImage != null)
        {
            hintImage.sprite = hintSprite;
            hintImage.enabled = hintSprite != null;
        }

        if (tipLabel != null)
            tipLabel.text = tipText;

        requireHintCloseBeforeDarkMode = true;
        OpenHintPanel();
    }

    private void OpenHintPanel()
    {
        if (hintPanel == null)
            return;

        if (isHintPanelOpen)
            return;

        FreezePlayerMovementForHint();
        SetHintPanelOpenState(true);

        if (hintAnimCoroutine != null)
            StopCoroutine(hintAnimCoroutine);

        hintAnimCoroutine = StartCoroutine(AnimateHintPanel(open: true));
    }

    public void CloseHintPanel(bool closedByEscape = false)
    {
        if (!isHintPanelOpen && (hintPanel == null || !hintPanel.activeSelf))
            return;

        if (closedByEscape)
            lastEscapeCloseFrame = Time.frameCount;

        requireHintCloseBeforeDarkMode = false;
        RestorePlayerMovementAfterHint();

        SetHintPanelOpenState(false);

        if (hintPanel == null)
            return;

        if (hintAnimCoroutine != null)
            StopCoroutine(hintAnimCoroutine);

        hintAnimCoroutine = StartCoroutine(AnimateHintPanel(open: false));
    }

    private IEnumerator AnimateHintPanel(bool open)
    {
        if (hintPanel == null)
        {
            hintAnimCoroutine = null;
            yield break;
        }

        hintPanel.SetActive(true);

        float duration = open ? openDuration : closeDuration;
        Vector3 from = new Vector3(open ? 0f : 1f, 1f, 1f);
        Vector3 to = new Vector3(open ? 1f : 0f, 1f, 1f);

        if (duration <= 0f)
        {
            hintPanel.transform.localScale = to;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                hintPanel.transform.localScale = Vector3.Lerp(from, to, t);
                yield return null;
            }

            hintPanel.transform.localScale = to;
        }

        if (!open)
            hintPanel.SetActive(false);

        hintAnimCoroutine = null;
    }

    private bool EnsureHintPanelBound()
    {
        if (hintPanel != null && hintPanelRect != null)
            return true;

        Transform panelTransform = null;
        if (!HasActiveSharedHintOwner())
            panelTransform = TryFindSharedHintPanel();

        if (panelTransform == null)
        {
            GameObject runtimeCanvas = GameObject.Find(RuntimeHintCanvasName);
            if (runtimeCanvas == null)
                runtimeCanvas = CreateRuntimeHintCanvas();

            if (runtimeCanvas != null)
                panelTransform = runtimeCanvas.transform.Find(HintPanelName);

            if (panelTransform == null && runtimeCanvas != null)
                panelTransform = CreateRuntimeHintPanel(runtimeCanvas.transform);
        }

        if (panelTransform == null)
            return false;

        return BindHintPanel(panelTransform);
    }

    private static bool HasActiveSharedHintOwner()
    {
        TutorialHintTrigger[] owners = FindObjectsOfType<TutorialHintTrigger>();
        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] != null && owners[i].isActiveAndEnabled)
                return true;
        }

        return false;
    }

    private static Transform TryFindSharedHintPanel()
    {
        GameObject sharedCanvas = GameObject.Find(SharedHintCanvasName);
        if (sharedCanvas == null)
            return null;

        return sharedCanvas.transform.Find(HintPanelName);
    }

    private bool BindHintPanel(Transform panelTransform)
    {
        hintPanel = panelTransform.gameObject;
        hintPanelRect = hintPanel.GetComponent<RectTransform>();
        hintImage = null;
        tipLabel = null;

        Image[] images = hintPanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].gameObject.name == "HintImage")
            {
                hintImage = images[i];
                break;
            }
        }

        Text[] texts = hintPanel.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "TipText")
            {
                tipLabel = texts[i];
                break;
            }

            if (texts[i].gameObject.name == "Text"
                && texts[i].transform.parent != null
                && texts[i].transform.parent.name == "TipText")
            {
                tipLabel = texts[i];
                break;
            }
        }

        if (tipLabel == null)
        {
            Transform tipRoot = hintPanel.transform.Find("ContentPanel/TipText");
            if (tipRoot != null)
                tipLabel = tipRoot.GetComponentInChildren<Text>(true);
        }

        if (hintPanelRect != null)
            hintPanelRect.pivot = new Vector2(0f, 0.5f);

        if (!hintPanel.activeSelf)
            hintPanel.transform.localScale = new Vector3(0f, 1f, 1f);

        return true;
    }

    private static GameObject CreateRuntimeHintCanvas()
    {
        GameObject canvasRoot = new GameObject(RuntimeHintCanvasName);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        canvasRoot.AddComponent<CanvasScaler>();
        canvasRoot.AddComponent<GraphicRaycaster>();
        return canvasRoot;
    }

    private static Transform CreateRuntimeHintPanel(Transform canvasTransform)
    {
        GameObject hintRoot = new GameObject(HintPanelName);
        hintRoot.transform.SetParent(canvasTransform, false);

        RectTransform hintRootRect = hintRoot.AddComponent<RectTransform>();
        SetFullStretch(hintRootRect);

        GameObject overlay = new GameObject("FullscreenOverlay");
        overlay.transform.SetParent(hintRoot.transform, false);
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        SetFullStretch(overlayRect);
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        GameObject contentPanel = new GameObject("ContentPanel");
        contentPanel.transform.SetParent(hintRoot.transform, false);
        RectTransform contentRect = contentPanel.AddComponent<RectTransform>();
        SetFullStretch(contentRect);
        contentPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.96f);

        GameObject hintImageGO = new GameObject("HintImage");
        hintImageGO.transform.SetParent(contentPanel.transform, false);
        RectTransform hintImageRect = hintImageGO.AddComponent<RectTransform>();
        SetFullStretch(hintImageRect);
        Image image = hintImageGO.AddComponent<Image>();
        image.preserveAspect = false;

        GameObject tipTextGO = new GameObject("TipText");
        tipTextGO.transform.SetParent(contentPanel.transform, false);
        RectTransform tipTextRect = tipTextGO.AddComponent<RectTransform>();
        tipTextRect.anchorMin = new Vector2(0f, 0f);
        tipTextRect.anchorMax = new Vector2(1f, 0f);
        tipTextRect.pivot = new Vector2(0.5f, 0f);
        tipTextRect.offsetMin = new Vector2(0f, 12f);
        tipTextRect.offsetMax = new Vector2(0f, 48f);

        Text text = tipTextGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(1f, 0.85f, 0.35f, 1f);
        text.text = string.Empty;

        hintRoot.SetActive(false);
        hintRoot.transform.localScale = new Vector3(0f, 1f, 1f);
        return hintRoot.transform;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ForceHideHintPanel()
    {
        if (hintAnimCoroutine != null)
        {
            StopCoroutine(hintAnimCoroutine);
            hintAnimCoroutine = null;
        }

        RestorePlayerMovementAfterHint();
        SetHintPanelOpenState(false);

        if (hintPanel == null)
            return;

        hintPanel.transform.localScale = new Vector3(0f, 1f, 1f);
        hintPanel.SetActive(false);
    }

    private void SetHintPanelOpenState(bool open)
    {
        if (isHintPanelOpen == open)
            return;

        isHintPanelOpen = open;

        if (open)
            openPanelCount++;
        else
            openPanelCount = Mathf.Max(0, openPanelCount - 1);
    }

    private void FreezePlayerMovementForHint()
    {
        if (hasFrozenPlayerForHint)
            return;

        if (playerRb == null)
            return;

        originalPlayerConstraints = playerRb.constraints;
        playerRb.velocity = Vector2.zero;
        playerRb.constraints = RigidbodyConstraints2D.FreezePositionX
                             | RigidbodyConstraints2D.FreezePositionY
                             | RigidbodyConstraints2D.FreezeRotation;
        hasFrozenPlayerForHint = true;
    }

    private void RestorePlayerMovementAfterHint()
    {
        if (!hasFrozenPlayerForHint)
            return;

        if (playerRb != null)
            playerRb.constraints = originalPlayerConstraints;

        hasFrozenPlayerForHint = false;
    }

    private void TryActivateDarkModeIfReady()
    {
        if (!activated || !activationAnimationCompleted)
            return;

        if (GameData.IsDarkModeActive)
            return;

        if (!MeetsDarkCondition())
            return;

        GameData.ActivateDarkMode();
        SaveManager.SaveCheckpoint();
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
        activationAnimationCompleted = false;
        requireHintCloseBeforeDarkMode = false;

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
        activationAnimationCompleted = true;
        activateCoroutine = null;

        if (playerInside)
            TryActivateDarkModeIfReady();
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
            activated = activated,
            hasShownHintOnce = hasShownHintOnce
        });
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState state = JsonUtility.FromJson<SnapshotState>(stateJson);
        activated = state != null && state.activated;
        hasShownHintOnce = state != null && state.hasShownHintOnce;
        activationAnimationCompleted = activated;
        pendingHintAfterAnimation = false;
        requireHintCloseBeforeDarkMode = false;
        playerInside = false;
        playerRb = null;

        if (activateCoroutine != null)
        {
            StopCoroutine(activateCoroutine);
            activateCoroutine = null;
        }

        ForceHideHintPanel();
        ApplyVisualState(activated);
    }
}
