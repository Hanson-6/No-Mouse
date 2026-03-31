using UnityEngine;
using GestureRecognition.Core;

// Push (open palm) : 玩家接触箱子时箱子跟随玩家速度
// Fist            : 抓取最近箱子，随玩家移动（kinematic MovePosition）
public class GestureInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private float grabRange = 0.8f;

    private SpriteRenderer playerSr;

    // Pull 状态
    private PushableBox activeBox;
    private Rigidbody2D activeRb;
    private float       offsetX;
    private float       floorY;

    void Start()
    {
        if (player != null)
            playerSr = player.GetComponent<SpriteRenderer>();
    }

    void OnEnable()  => GestureEvents.OnGestureUpdated += OnGesture;
    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGesture;
        ReleaseGrab();
        PushableBox.PushEnabledFrame = -1;
    }

    void OnGesture(GestureResult result)
    {
        switch (result.Type)
        {
            case GestureType.Push:
                ReleaseGrab();                              // 先释放拉
                PushableBox.PushEnabledFrame = Time.frameCount; // 每帧刷新
                break;

            case GestureType.Fist:
                PushableBox.PushEnabledFrame = -1;          // 关闭推
                if (activeBox == null) TryGrab();
                break;

            default:
                ReleaseGrab();
                PushableBox.PushEnabledFrame = -1;
                break;
        }
    }

    // ── Pull: kinematic MovePosition ─────────────────────────────────────────

    void TryGrab()
    {
        if (player == null) return;

        var hits = Physics2D.OverlapCircleAll(player.transform.position, grabRange);
        PushableBox best     = null;
        float       bestDist = float.MaxValue;

        foreach (var col in hits)
        {
            if (!col.CompareTag("Box")) continue;
            var box = col.GetComponent<PushableBox>();
            if (box == null) continue;
            float d = Vector2.Distance(player.transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = box; }
        }

        if (best == null) return;

        best.Grab();
        offsetX = best.transform.position.x - player.transform.position.x;
        floorY  = best.transform.position.y;
        activeBox = best;
        activeRb  = best.Rb;
        player.jumpLocked = true;
    }

    void ReleaseGrab()
    {
        if (activeBox == null) return;
        activeBox.Release();
        activeBox = null;
        activeRb  = null;
        if (player != null) player.jumpLocked = false;
    }

    void FixedUpdate()
    {
        // Push 激活时禁止跳跃（手势停止触发后 PushActive 自动失效，jumpLocked 也自动解除）
        if (activeBox == null && player != null)
            player.jumpLocked = PushableBox.PushActive;

        if (activeBox == null || activeRb == null || player == null) return;

        // Pull: 只跟随 X 轴，Y 保持箱子当前高度（不锁死在初始 floorY）
        float targetX = player.transform.position.x + offsetX;
        activeRb.MovePosition(new Vector2(targetX, activeBox.transform.position.y));
    }
}
