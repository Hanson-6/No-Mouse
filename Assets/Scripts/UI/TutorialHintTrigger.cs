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

        // 自动查找场景中的 HintPanel
        var canvas = GameObject.Find("HintCanvas");
        if (canvas != null)
        {
            var hintRootTf = canvas.transform.Find("HintPanel");
            if (hintRootTf != null)
            {
                hintPanel = hintRootTf.gameObject;
                hintPanelRect = hintPanel.GetComponent<RectTransform>();

                foreach (var img in hintPanel.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject.name == "HintImage") { hintImage = img; break; }
                }
                foreach (var txt in hintPanel.GetComponentsInChildren<Text>(true))
                {
                    if (txt.gameObject.name == "TipText") { tipLabel = txt; break; }
                    if (txt.gameObject.name == "Text" && txt.transform.parent != null && txt.transform.parent.name == "TipText")
                    {
                        tipLabel = txt;
                        break;
                    }
                }

                if (tipLabel == null)
                {
                    var tipRoot = hintPanel.transform.Find("ContentPanel/TipText");
                    if (tipRoot != null)
                        tipLabel = tipRoot.GetComponentInChildren<Text>(true);
                }
            }
        }

        if (hintPanel == null)
            Debug.LogWarning("[TutorialHintTrigger] 找不到 HintPanel，请先运行 Tools/Tutoring/1. Setup Tutoring UI。");

        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
            // 初始 pivot 设在左边，从左向右展开
            if (hintPanelRect != null)
                hintPanelRect.pivot = new Vector2(0f, 0.5f);
        }
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
