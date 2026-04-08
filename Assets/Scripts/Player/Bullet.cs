using UnityEngine;

/// <summary>
/// 子弹：水平飞行，击中敌人时杀死敌人，击中其他任何物体（地形、箱子、平台等）时销毁自身。
/// 忽略 Player 和其他 Bullet 的碰撞。
///
/// 设计思路（方案 B — 黑名单）：
///   碰到 Enemy      → enemy.Die() + 销毁自身（一发一个，不穿透）
///   碰到 Player     → 忽略（自己发射的子弹不应该伤害自己）
///   碰到另一个 Bullet → 忽略（多发子弹不互相抵消）
///   碰到其他任何东西  → 销毁自身（地面、墙壁、箱子、移动平台、锯子、门等）
///
/// Inspector 可调参数说明：
///   Bullet Sprite       — 子弹图片，留空则显示红色圆形
///   Bullet Color        — 子弹颜色（白色 = 图片原色，改颜色不需要换图片）
///   Target World Height — 子弹在世界中的高度（Unity Units），Player 约 0.32
///   Speed               — 飞行速度（Unity Units/秒）
///   Lifetime            — 子弹最长存活秒数（超时自动销毁）
///   Trail Time          — 拖尾持续时间（秒），越大拖尾越长
///   Trail Start Color   — 拖尾起始颜色（子弹屁股处）
///   Trail End Color     — 拖尾末尾颜色（通常设为全透明）
///   Trail Start Width   — 拖尾起始宽度（-1 = 自动跟随子弹大小）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    // ── 外观 ─────────────────────────────────────────────────────────────────

    [Header("外观")]

    // 子弹精灵图。留空则显示红色圆形占位图。
    [SerializeField] private Sprite bulletSprite;

    // 子弹颜色。白色(1,1,1,1) = 图片原色。
    // 改颜色不需要换图片，直接在 Inspector 点色块即可。
    // 注意：这也会影响拖尾颜色（拖尾颜色基于此值自动计算）。
    [SerializeField] private Color bulletColor = Color.white;

    // 子弹在世界空间中的目标高度（Unity Units）。
    // Player 高度约 0.32，默认 0.12 让子弹约为 Player 的 37%。
    // 脚本会根据精灵图实际分辨率自动计算 localScale，
    // 换任何精灵图都不需要手动调整 Scale。
    [SerializeField] private float targetWorldHeight = 0.12f;

    // ── 移动 ─────────────────────────────────────────────────────────────────

    [Header("移动")]

    // 飞行速度（Unity Units/秒）。Player 宽约 0.32，速度 12 意味着子弹
    // 每秒飞过约 37 个 Player 宽度，视觉上非常快。
    [SerializeField] public float speed = 12f;

    // 子弹最长存活时间（秒）。超时自动销毁，防止飞出屏幕后永远存在。
    [SerializeField] public float lifetime = 2.5f;

    // ── 拖尾 ─────────────────────────────────────────────────────────────────

    [Header("拖尾")]

    // 拖尾持续时间（秒）。值越大拖尾越长。
    // 例：trailTime=0.15, speed=12 → 拖尾约 1.8 units 长。
    [SerializeField] private float trailTime = 0.15f;

    // 拖尾起始颜色（子弹屁股处）。Alpha 决定不透明度。
    // 默认值：与子弹同色，60% 不透明。
    [SerializeField] private Color trailStartColor = new Color(1f, 0.8f, 0.2f, 0.8f);

    // 拖尾末尾颜色（拖尾末端）。通常设为全透明，形成渐隐效果。
    [SerializeField] private Color trailEndColor = new Color(1f, 0.5f, 0f, 0f);

    // 拖尾起始宽度（Unity Units）。
    // -1 表示自动计算（= targetWorldHeight × 0.4）。
    // 设为正数则使用固定宽度。
    [SerializeField] private float trailStartWidth = -1f;

    // ── 私有状态 ─────────────────────────────────────────────────────────────

    private float direction;

    // ── 生命周期 ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();

        // ── 精灵图赋值 ──────────────────────────────────────────────────
        if (bulletSprite != null)
        {
            sr.sprite = bulletSprite;
        }
        else if (sr.sprite == null)
        {
            // fallback：运行时生成红色小圆
            sr.sprite = MakeCircleSprite(32, Color.red);
        }

        // ── 颜色赋值 ────────────────────────────────────────────────────
        // bulletColor 默认白色 = 图片原色；改颜色无需换图。
        sr.color = bulletColor;

        // ── 按目标世界高度缩放 ──────────────────────────────────────────
        // 不管精灵图多大（64px 或 600px），都缩放到 targetWorldHeight。
        if (sr.sprite != null)
        {
            float nativeHeight = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
            transform.localScale = Vector3.one * (targetWorldHeight / nativeHeight);
        }
        else
        {
            transform.localScale = Vector3.one * 0.25f;
        }

        // ── 渲染排序：确保渲染在 Player 前面 ───────────────────────────
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            var psr = playerGO.GetComponent<SpriteRenderer>();
            if (psr != null)
            {
                sr.sortingLayerName = psr.sortingLayerName;
                sr.sortingOrder     = psr.sortingOrder + 1;
            }
        }

        // ── 拖尾特效 ────────────────────────────────────────────────────
        SetupTrail(sr);
    }

    // ── 拖尾设置 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 在运行时添加 TrailRenderer 组件，参数全部来自 Inspector 字段，
    /// 无需在 Prefab 上手动添加 TrailRenderer。
    /// </summary>
    void SetupTrail(SpriteRenderer sr)
    {
        var trail = gameObject.AddComponent<TrailRenderer>();

        // 拖尾持续时间（越大越长）
        trail.time = trailTime;

        // 拖尾宽度：trailStartWidth=-1 时自动跟随子弹大小
        float sw = (trailStartWidth < 0f) ? (targetWorldHeight * 0.4f) : trailStartWidth;
        trail.startWidth = sw;
        trail.endWidth   = 0.005f;

        // 顶点精度
        trail.numCornerVertices = 0;
        trail.numCapVertices    = 2;
        trail.minVertexDistance = 0.05f;

        // 材质：与 SpriteRenderer 共用同一材质，确保渲染管线兼容
        trail.material = new Material(sr.sharedMaterial);

        // 颜色：直接使用 Inspector 中设置的 trailStartColor / trailEndColor
        trail.startColor = trailStartColor;
        trail.endColor   = trailEndColor;

        // 排序：与子弹同层，但在子弹后面（拖尾不遮挡子弹本体）
        trail.sortingLayerName = sr.sortingLayerName;
        trail.sortingOrder     = sr.sortingOrder - 1;
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 生成一个纯色圆形 Sprite 作为 fallback（没有精灵图时使用）。
    /// </summary>
    static Sprite MakeCircleSprite(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                tex.SetPixel(x, y, (dx * dx + dy * dy <= r * r) ? color : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ── 公开接口 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 由 ShootingController.Fire() 调用，设置飞行方向并启动自毁倒计时。
    /// dir > 0 = 向右飞，dir < 0 = 向左飞
    /// </summary>
    public void Init(float dir)
    {
        direction = Mathf.Sign(dir);

        // 往左飞时翻转 Sprite，使图片朝向正确
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && direction < 0f)
            sr.flipX = true;

        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        transform.Translate(Vector2.right * direction * speed * Time.fixedDeltaTime);
    }

    // ── 碰撞检测 ─────────────────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        // 忽略 Player（自己发射的子弹不伤害自己）
        if (other.CompareTag("Player")) return;

        // 忽略其他 Bullet（多发子弹不互相抵消）
        if (other.GetComponent<Bullet>() != null) return;

        // 击中 Enemy → 杀死敌人 + 销毁自身
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Die();
            Destroy(gameObject);
            return;
        }

        // 击中其他任何东西（地形、箱子、平台、锯子、门等）→ 销毁自身
        Destroy(gameObject);
    }
}
