using UnityEngine;
using GestureRecognition.Core;

// 精灵手显示系统
//
// 功能：
//   当 GestureService 检测到 Push（张开手掌）或 Fist（握拳）手势时，
//   在 Player 头顶显示对应的精灵手图标，并以脉动效果（Scale + Alpha 循环）
//   提醒玩家当前手势已被系统识别。
//
// 生命周期：
//   手势出现 → 精灵手淡入并开始脉动
//   手势消失 / 切换到其他手势 → 精灵手立即隐藏
//
// 朝向：
//   精灵手的 flipX 跟随 PlayerController.FacingRight，
//   始终与 Player 面朝方向一致。
//
// 挂载：
//   挂在场景中一个独立的 GameObject 上（不是 Player 的子物体，
//   避免随 Player flipX 翻转影响位置偏移计算）。
//   在 Inspector 中拖入 player 引用。
public class SpiritHandDisplay : MonoBehaviour
{
    [Header("精灵图")]
    [SerializeField] private Sprite pushSprite;   // Push 手势对应的图（张开手掌）
    [SerializeField] private Sprite fistSprite;   // Fist 手势对应的图（握拳 / Pull）

    [Header("跟随目标")]
    [SerializeField] private PlayerController player;

    [Header("位置偏移")]
    // 相对于 Player 中心的偏移。Player 高度约 0.32 Units，
    // 默认 0.35 让精灵手刚好悬浮在头顶上方一点点。
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.35f);

    [Header("显示尺寸")]
    // 精灵手在世界空间中的目标高度（Unity Units）。
    // Player 高度约 0.32，默认 0.2 让精灵手略小于 Player。
    // 脚本会根据图片实际分辨率自动计算 localScale。
    [SerializeField] private float targetWorldHeight = 0.2f;

    [Header("脉动参数")]
    // 脉动速度（弧度/秒）。3.0 ≈ 约 2 秒一个完整呼吸周期。
    [SerializeField] private float pulseSpeed = 3f;
    // Scale 脉动幅度（相对于基准缩放的倍率变化范围）
    [SerializeField] private float minPulseScale = 0.95f;
    [SerializeField] private float maxPulseScale = 1.05f;
    // Alpha 在 [minAlpha, maxAlpha] 之间循环
    [SerializeField] private float minAlpha = 0.65f;
    [SerializeField] private float maxAlpha = 1.0f;

    // ── 私有状态 ─────────────────────────────────────────────────────────────

    private SpriteRenderer _sr;
    private float _pulseTimer;
    private bool _visible;          // 当前是否应该显示精灵手
    private GestureType _currentGestureType = GestureType.None;
    private float _baseScale = 1f;  // 根据图片分辨率计算出的基准缩放

    // ── 生命周期 ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        // 如果 Inspector 没拖入 Player，自动查找
        if (player == null)
            player = FindObjectOfType<PlayerController>();

        // 计算基准缩放：让精灵手在世界空间中的高度 = targetWorldHeight
        RecalculateBaseScale(pushSprite);

        // 初始状态：隐藏
        _visible = false;
        _sr.color = new Color(1f, 1f, 1f, 0f);
        transform.localScale = Vector3.one * _baseScale;
    }

    void OnEnable()
    {
        GestureEvents.OnGestureUpdated += OnGestureUpdated;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGestureUpdated;
        Hide();
    }

    // ── 手势事件回调 ─────────────────────────────────────────────────────────

    void OnGestureUpdated(GestureResult result)
    {
        _currentGestureType = result.Type;

        switch (result.Type)
        {
            case GestureType.Push:
                _sr.sprite = pushSprite;
                RecalculateBaseScale(pushSprite);
                Show();
                break;

            case GestureType.Fist:
                _sr.sprite = fistSprite;
                RecalculateBaseScale(fistSprite);
                Show();
                break;

            default:
                Hide();
                break;
        }
    }

    // ── 每帧更新 ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (player == null) return;

        // 跟随 Player 头顶位置
        transform.position = (Vector2)player.transform.position + offset;

        // 朝向跟随 Player 面朝方向
        _sr.flipX = !player.FacingRight;

        if (!_visible) return;

        // 脉动：Sin 函数驱动 Scale 和 Alpha 同步循环
        // _pulseTimer 持续累加，不在 Show()/Hide() 时重置，
        // 这样切换手势时脉动节奏不会突变。
        _pulseTimer += Time.deltaTime * pulseSpeed;
        float t = Mathf.Sin(_pulseTimer) * 0.5f + 0.5f; // 将 Sin(-1~1) 映射到 (0~1)

        float scale = _baseScale * Mathf.Lerp(minPulseScale, maxPulseScale, t);
        transform.localScale = Vector3.one * scale;

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        _sr.color = new Color(1f, 1f, 1f, alpha);
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 根据 Sprite 实际像素高度和 PPU 计算 localScale，
    /// 使精灵手在世界空间中刚好等于 targetWorldHeight。
    /// </summary>
    void RecalculateBaseScale(Sprite sprite)
    {
        if (sprite == null) { _baseScale = 1f; return; }
        // Sprite 在 localScale=1 时的世界高度 = pixelHeight / pixelsPerUnit
        float nativeHeight = sprite.rect.height / sprite.pixelsPerUnit;
        _baseScale = targetWorldHeight / nativeHeight;
    }

    void Show()
    {
        _visible = true;
        // 立即设为可见（脉动会在 Update 中接管 Alpha）
        _sr.color = new Color(1f, 1f, 1f, minAlpha);
    }

    void Hide()
    {
        _visible = false;
        _sr.color = new Color(1f, 1f, 1f, 0f);
        transform.localScale = Vector3.one * _baseScale;
    }
}
