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

    // Runtime references — found automatically
    private SpriteRenderer sr;
    private Vector3 unpressedPos;
    private Vector3 pressedPos;
    private int overlapCount;
    private bool isPanelOpen;

    private GameObject hintPanel;
    private Image hintImage;
    private Button closeButton;

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

        // 自动查找场景中的 HintPanel（由 TutoringSetup 一次性创建）
        var hintRoot = GameObject.Find("HintPanel");
        if (hintRoot != null)
        {
            hintPanel   = hintRoot;
            hintImage   = hintRoot.GetComponentInChildren<Image>(true);
            closeButton = hintRoot.GetComponentInChildren<Button>(true);

            // 找名字叫 HintImage 的 Image（避免拿到背景的 Image）
            foreach (var img in hintRoot.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject.name == "HintImage")
                {
                    hintImage = img;
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("[TutorialHintTrigger] 找不到 HintPanel，请先运行 Tools/Setup Tutoring UI。");
        }

        if (hintPanel != null)
            hintPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
    }

    void Update()
    {
        if (isPanelOpen && Input.anyKeyDown && !Input.GetMouseButtonDown(0))
            ClosePanel();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        overlapCount++;
        if (overlapCount == 1) Press();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        overlapCount = Mathf.Max(0, overlapCount - 1);
        if (overlapCount == 0) Release();
    }

    void Press()
    {
        transform.position = pressedPos;
        if (sr != null && pressedSprite != null)
            sr.sprite = pressedSprite;

        if (hintPanel == null) return;
        if (hintImage != null && hintSprite != null)
            hintImage.sprite = hintSprite;
        hintPanel.SetActive(true);
        isPanelOpen = true;
    }

    void Release()
    {
        transform.position = unpressedPos;
        if (sr != null && unpressedSprite != null)
            sr.sprite = unpressedSprite;
    }

    public void ClosePanel()
    {
        if (hintPanel == null) return;
        hintPanel.SetActive(false);
        isPanelOpen = false;
    }
}
