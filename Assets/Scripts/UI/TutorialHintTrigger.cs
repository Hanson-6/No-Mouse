using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Pressure-plate style tutorial hint.
// Finds HintPanel automatically at runtime — no Inspector wiring needed.
// Place this prefab anywhere in the scene; set hintSprite in the Inspector.
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialHintTrigger : MonoBehaviour
{
    private const string SharedHintCanvasName = "HintCanvas";
    private const string RuntimeHintCanvasName = "TutorialHintCanvas";
    private const string HintPanelName = "HintPanel";

    [Header("Sprites")]
    [SerializeField] private Sprite unpressedSprite;
    [SerializeField] private Sprite pressedSprite;

    [Header("Press Animation")]
    [SerializeField] private float pressDownAmount = 0.15f;

    [Header("Hint Content")]
    [SerializeField] private Sprite hintSprite;
    [SerializeField] private string tipText = "Press Esc to quit";

    [Header("Panel Animation")]
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField] private float closeDuration = 0.15f;

    // Runtime references — found automatically
    private SpriteRenderer sr;
    private Vector3 unpressedPos;
    private Vector3 pressedPos;
    private readonly HashSet<int> activePressers = new HashSet<int>();
    private bool isPanelOpen;

    public static bool IsAnyHintPanelOpen => openPanelCount > 0;
    public static bool DidConsumeEscapeThisFrame => lastEscapeCloseFrame == Time.frameCount;

    private static int openPanelCount;
    private static int lastEscapeCloseFrame = -1;

    private GameObject hintPanel;
    private RectTransform hintPanelRect;
    private Image hintImage;
    private Text tipLabel;
    private Coroutine animCoroutine;

    // Player freeze
    private Rigidbody2D playerRb;
    private RigidbodyConstraints2D originalConstraints;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        unpressedPos = transform.position;
        pressedPos   = unpressedPos + Vector3.down * pressDownAmount;
    }

    void Start()
    {
        if (sr != null && unpressedSprite != null)
            sr.sprite = unpressedSprite;

        EnsureHintPanelBound();

        if (hintPanel == null)
            Debug.LogWarning("[TutorialHintTrigger] 找不到 HintPanel，且运行时创建失败。请检查 Canvas/UI 组件是否可用。");

        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
            // 初始 pivot 设在左边，从左向右展开
            if (hintPanelRect != null)
                hintPanelRect.pivot = new Vector2(0f, 0.5f);

            hintPanel.transform.localScale = new Vector3(0f, 1f, 1f);
        }
    }

    private bool EnsureHintPanelBound()
    {
        if (hintPanel != null && hintPanelRect != null)
            return true;

        Transform panelTransform = TryFindSharedHintPanel();
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

        foreach (Image img in hintPanel.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name == "HintImage")
            {
                hintImage = img;
                break;
            }
        }

        foreach (Text txt in hintPanel.GetComponentsInChildren<Text>(true))
        {
            if (txt.gameObject.name == "TipText")
            {
                tipLabel = txt;
                break;
            }

            if (txt.gameObject.name == "Text"
                && txt.transform.parent != null
                && txt.transform.parent.name == "TipText")
            {
                tipLabel = txt;
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

        return true;
    }

    private static GameObject CreateRuntimeHintCanvas()
    {
        GameObject canvasRoot = new GameObject(RuntimeHintCanvasName);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

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

    void Update()
    {
        if (isPanelOpen && Input.GetKeyDown(KeyCode.Escape))
            ClosePanel(closedByEscape: true);
    }

    void OnDisable()
    {
        SetPanelOpenState(false);

        if (playerRb != null)
            playerRb.constraints = originalConstraints;

        activePressers.Clear();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryRegisterPresser(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // Handles the case where player enters trigger while airborne,
        // then lands inside without triggering a new OnTriggerEnter2D.
        TryRegisterPresser(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!activePressers.Remove(other.GetInstanceID())) return;
        if (activePressers.Count == 0) Release();
    }

    void TryRegisterPresser(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        int id = other.GetInstanceID();
        if (activePressers.Contains(id)) return;

        var pc = other.GetComponent<PlayerController>();
        if (pc != null && !pc.IsGrounded) return;

        activePressers.Add(id);
        if (activePressers.Count != 1) return;

        playerRb = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody2D>();
        Press();
    }

    void Press()
    {
        transform.position = pressedPos;
        if (sr != null && pressedSprite != null)
            sr.sprite = pressedSprite;

        // 冻结玩家移动
        if (playerRb != null)
        {
            originalConstraints = playerRb.constraints;
            playerRb.velocity = Vector2.zero;
            playerRb.constraints = RigidbodyConstraints2D.FreezePositionX
                                 | RigidbodyConstraints2D.FreezePositionY
                                 | RigidbodyConstraints2D.FreezeRotation;
        }

        if (hintPanel == null) return;
        if (hintImage != null && hintSprite != null)
            hintImage.sprite = hintSprite;
        if (tipLabel != null)
            tipLabel.text = tipText;

        SetPanelOpenState(true);
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimatePanel(open: true));
    }

    void Release()
    {
        transform.position = unpressedPos;
        if (sr != null && unpressedSprite != null)
            sr.sprite = unpressedSprite;
    }

    public void ClosePanel(bool closedByEscape = false)
    {
        if (hintPanel == null) return;
        SetPanelOpenState(false);

        if (closedByEscape)
            lastEscapeCloseFrame = Time.frameCount;

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimatePanel(open: false));

        // 恢复玩家移动
        if (playerRb != null) playerRb.constraints = originalConstraints;
    }

    // 从左到右展开 / 从右到左收起
    IEnumerator AnimatePanel(bool open)
    {
        hintPanel.SetActive(true);

        float duration = open ? openDuration : closeDuration;
        float elapsed = 0f;
        Vector3 from = new Vector3(open ? 0f : 1f, 1f, 1f);
        Vector3 to   = new Vector3(open ? 1f : 0f, 1f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            hintPanel.transform.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        hintPanel.transform.localScale = to;

        if (!open)
            hintPanel.SetActive(false);
    }

    private void SetPanelOpenState(bool open)
    {
        if (isPanelOpen == open)
            return;

        isPanelOpen = open;

        if (open)
            openPanelCount++;
        else
            openPanelCount = Mathf.Max(0, openPanelCount - 1);
    }
}
