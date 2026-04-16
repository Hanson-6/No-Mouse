using System.Collections.Generic;
using UnityEngine;
using GestureRecognition.Core;
using GestureRecognition.Service;

// 精灵手显示系统
//
// 功能：
//   当 GestureService 检测到 Push（张开手掌）、Fist（握拳）、Shoot（手枪）、
//   Switch（一拳一掌切换）或 InvulnerableBody（金身）手势时，
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
    private static readonly GestureType[] SupportedGestures =
    {
        GestureType.Push,
        GestureType.Fist,
        GestureType.Shoot,
        GestureType.Switch,
        GestureType.InvulnerableBody
    };

    [System.Serializable]
    private class GestureSizeEntry
    {
        public GestureType gesture = GestureType.None;
        public float sizeMultiplier = 1f;
    }

    [Header("精灵图")]
    [SerializeField] private Sprite pushSprite;   // Push 手势对应的图（张开手掌）
    [SerializeField] private Sprite fistSprite;   // Fist 手势对应的图（握拳 / Pull）
    [SerializeField] private Sprite shootSprite;  // Shoot 手势对应的图（手枪手势）
    [SerializeField] private Sprite switchSprite; // Switch 手势对应的图（位置切换）
    [SerializeField] private Sprite invulnerableBodySprite; // 金身手势对应的图

    [Header("手势大小倍率（输入值，默认 1.0）")]
    [SerializeField] private List<GestureSizeEntry> gestureSizeEntries = new List<GestureSizeEntry>();

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
    private float _gestureScaleMultiplier = 1f;
    private GestureConfig _fallbackConfig;
    private readonly Dictionary<GestureType, float> _gestureScaleLookup = new Dictionary<GestureType, float>();

    void OnValidate()
    {
        EnsureGestureSizeEntries();
    }

    // ── 生命周期 ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        // 如果 Inspector 没拖入 Player，自动查找
        if (player == null)
            player = FindObjectOfType<PlayerController>();

        // 计算基准缩放：让精灵手在世界空间中的高度 = targetWorldHeight
        RecalculateBaseScale(pushSprite);
        _fallbackConfig = Resources.Load<GestureConfig>("GestureConfig");
        EnsureGestureSizeEntries();

        // 初始状态：隐藏
        _visible = false;
        _sr.color = new Color(1f, 1f, 1f, 0f);
        transform.localScale = Vector3.one * _baseScale;
    }

    void OnEnable()
    {
        if (player == null)
            player = FindObjectOfType<PlayerController>();
        GestureEvents.OnGestureUpdated += OnGestureUpdated;
    }

    public void SetPlayer(PlayerController target)
    {
        player = target;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGestureUpdated;
        Hide();
    }

    // ── 手势事件回调 ─────────────────────────────────────────────────────────

    void OnGestureUpdated(GestureResult result)
    {
        if (ShouldHideForDualDifferentHands(result.Type))
        {
            Hide();
            return;
        }

        _currentGestureType = result.Type;

        if (!IsSupportedGesture(result.Type))
        {
            Hide();
            return;
        }

        Sprite resolvedSprite = ResolveSprite(result.Type);
        if (resolvedSprite == null)
        {
            Hide();
            return;
        }

        _gestureScaleMultiplier = GetGestureScaleMultiplier(result.Type);
        _sr.sprite = resolvedSprite;
        RecalculateBaseScale(resolvedSprite);
        Show();
    }

    bool ShouldHideForDualDifferentHands(GestureType detectedType)
    {
        if (detectedType == GestureType.Switch)
            return false;

        GestureService service = GestureService.Instance;
        if (service == null)
            return false;

        if (!service.HasLeftHandSlot || !service.HasRightHandSlot)
            return false;

        return service.LeftHandGestureType != service.RightHandGestureType;
    }

    Sprite ResolveSprite(GestureType type)
    {
        switch (type)
        {
            case GestureType.Push:
                if (pushSprite != null) return pushSprite;
                if (_fallbackConfig != null)
                {
                    Sprite push = _fallbackConfig.GetSprite(GestureType.Push);
                    if (push != null) return push;
                }
                return null;

            case GestureType.Fist:
                if (fistSprite != null) return fistSprite;
                break;

            case GestureType.Shoot:
                if (shootSprite != null) return shootSprite;
                break;

            case GestureType.Switch:
                if (switchSprite != null) return switchSprite;
                break;

            case GestureType.InvulnerableBody:
                if (invulnerableBodySprite != null) return invulnerableBodySprite;
                break;

            default:
                break;
        }

        return _fallbackConfig != null ? _fallbackConfig.GetSprite(type) : null;
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

        float scale = _baseScale * _gestureScaleMultiplier * Mathf.Lerp(minPulseScale, maxPulseScale, t);
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
        // ★ BUG FIX: 旧代码在 sprite==null 时将 _baseScale 设为 1.0，
        // 这会导致下次切换到有效精灵图时，localScale 仍是 ~1.0，
        // 精灵图以原始尺寸（3.5+ units）渲染一帧 → "闪大"。
        // 修复：sprite 为 null 时不修改 _baseScale，保留上一个有效值。
        if (sprite == null) return;
        // Sprite 在 localScale=1 时的世界高度 = pixelHeight / pixelsPerUnit
        float nativeHeight = sprite.rect.height / sprite.pixelsPerUnit;
        _baseScale = targetWorldHeight / nativeHeight;
    }

    void Show()
    {
        _visible = true;
        // 立即设为可见（脉动会在 Update 中接管 Alpha）
        _sr.color = new Color(1f, 1f, 1f, minAlpha);

        // ★ BUG FIX: 旧代码没有在 Show() 中更新 localScale，
        // 导致切换精灵图后、Update() 运行之前，有一帧使用旧的 localScale。
        // 如果旧 _baseScale 远大于新值（如 null sprite 时 _baseScale=1.0），
        // 精灵图会以原始巨大尺寸闪一帧。
        // 修复：Show() 中立即同步 localScale。
        transform.localScale = Vector3.one * (_baseScale * _gestureScaleMultiplier);
    }

    void Hide()
    {
        _visible = false;
        _sr.color = new Color(1f, 1f, 1f, 0f);
        transform.localScale = Vector3.one * (_baseScale * _gestureScaleMultiplier);
    }

    void EnsureGestureSizeEntries()
    {
        if (gestureSizeEntries == null)
            gestureSizeEntries = new List<GestureSizeEntry>();

        for (int i = gestureSizeEntries.Count - 1; i >= 0; i--)
        {
            GestureSizeEntry entry = gestureSizeEntries[i];
            if (entry == null || entry.gesture == GestureType.None || entry.gesture == GestureType.Count || !IsSupportedGesture(entry.gesture))
                gestureSizeEntries.RemoveAt(i);
        }

        for (int i = 0; i < SupportedGestures.Length; i++)
        {
            GestureType gesture = SupportedGestures[i];
            bool exists = false;
            for (int j = 0; j < gestureSizeEntries.Count; j++)
            {
                if (gestureSizeEntries[j].gesture == gesture)
                {
                    exists = true;
                    if (gestureSizeEntries[j].sizeMultiplier <= 0f)
                        gestureSizeEntries[j].sizeMultiplier = 1f;
                    break;
                }
            }

            if (!exists)
            {
                gestureSizeEntries.Add(new GestureSizeEntry
                {
                    gesture = gesture,
                    sizeMultiplier = 1f
                });
            }
        }

        gestureSizeEntries.Sort((a, b) => a.gesture.CompareTo(b.gesture));
        RebuildGestureScaleLookup();
    }

    void RebuildGestureScaleLookup()
    {
        _gestureScaleLookup.Clear();

        for (int i = 0; i < gestureSizeEntries.Count; i++)
        {
            GestureSizeEntry entry = gestureSizeEntries[i];
            if (entry == null) continue;
            float multiplier = entry.sizeMultiplier <= 0f ? 1f : entry.sizeMultiplier;
            _gestureScaleLookup[entry.gesture] = multiplier;
        }
    }

    float GetGestureScaleMultiplier(GestureType type)
    {
        if (_gestureScaleLookup.TryGetValue(type, out float multiplier) && multiplier > 0f)
            return multiplier;
        return 1f;
    }

    bool IsSupportedGesture(GestureType type)
    {
        for (int i = 0; i < SupportedGestures.Length; i++)
        {
            if (SupportedGestures[i] == type)
                return true;
        }

        return false;
    }
}
