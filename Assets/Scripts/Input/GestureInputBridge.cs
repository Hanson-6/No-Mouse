using UnityEngine;
using GestureRecognition.Core;

// Push（张开手掌）：玩家可以自由移动（包括反方向），Box 只在玩家往推方向移动时跟随
// Pull（握拳）    ：玩家面朝方向锁定，只能往拉的方向（面朝反方向）移动，Box 跟随
//
// 触发条件（Push 和 Pull 相同，3 个都要满足）：
//   1. 玩家做出对应手势
//   2. 玩家与箱子发生水平方向碰撞（不是从上面跳上去的）
//   3. 玩家面朝箱子（spriteRenderer.flipX 与 Box 相对位置一致）
//
// 连接建立后：
//   Push 时 Player 自由移动（moveDirection = 0），Box 只在推方向时跟随
//   Pull 时 Player 只能往面朝反方向走，且面朝方向锁定
//
// 断开条件：
//   手势消失（帧级别判断）/ Player 和 Box 距离超过阈值
public class GestureInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    // 距离断开阈值：Player 和 Box 中心距离超过这个值时断开连接
    // Player 宽度 ≈ 0.32, Box 宽度 ≈ 0.32, 紧贴时中心距 ≈ 0.32
    // 留一些余量（0.6），这样 Pull 时 Player 远离一小段仍保持连接
    [SerializeField] private float unlinkDistance = 2f;

    // 当前手势类型
    private GestureType currentGesture = GestureType.None;

    // 当前已连接的箱子（Push 或 Pull 进行中）
    private PushableBox linkedBox;

    void Awake()
    {
        if (player == null)
            player = FindObjectOfType<PlayerController>();
    }

    public void SetPlayer(PlayerController target)
    {
        player = target;
    }

    void OnEnable()
    {
        GestureEvents.OnGestureUpdated += OnGesture;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGesture;
        UnlinkAndClear();
        PushableBox.PushEnabledFrame = -1;
        PushableBox.PullEnabledFrame = -1;
    }

    void OnGesture(GestureResult result)
    {
        // 手势切换时，清除另一种模式的帧标记，并断开旧连接
        GestureType prev = currentGesture;
        currentGesture = result.Type;

        switch (result.Type)
        {
            case GestureType.Push:
                PushableBox.PullEnabledFrame = -1;
                if (prev != GestureType.Push) UnlinkAndClear();
                break;

            case GestureType.Fist:
                PushableBox.PushEnabledFrame = -1;
                if (prev != GestureType.Fist) UnlinkAndClear();
                break;

            default:
                UnlinkAndClear();
                PushableBox.PushEnabledFrame = -1;
                PushableBox.PullEnabledFrame = -1;
                break;
        }
    }

    // ── 连接管理 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 尝试与正在水平接触且玩家面朝的箱子建立连接。
    /// 条件：IsTouchingPlayer + 玩家面朝 Box。
    /// </summary>
    bool TryLink(BoxLinkMode mode)
    {
        if (player == null) return false;

        foreach (var box in PushableBox.AllBoxes)
        {
            if (!box.IsTouchingPlayer) continue;

            // 判断玩家是否面朝 Box
            // Box 在 Player 右边 → Player 必须面朝右（FacingRight = true）
            // Box 在 Player 左边 → Player 必须面朝左（FacingRight = false）
            float dx = box.transform.position.x - player.transform.position.x;
            bool boxOnRight = dx > 0f;
            if (boxOnRight != player.FacingRight) continue; // 没有面朝 Box，跳过

            box.Link(mode, player.FacingRight);
            linkedBox = box;
            return true;
        }
        return false;
    }

    /// <summary>断开连接，解除 Player 上的所有锁定状态。</summary>
    void UnlinkAndClear()
    {
        if (linkedBox != null)
        {
            linkedBox.Unlink();
            linkedBox = null;
        }
        if (player != null)
        {
            player.facingLocked = false;
            player.moveDirection = 0;
        }
    }

    // ── 物理帧更新 ──────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (player == null) return;

        // 玩家离地（例如起跳）时，立即断开箱子连接，避免出现“隔空推/拉”。
        if (!player.IsGrounded)
        {
            if (linkedBox != null)
                UnlinkAndClear();

            PushableBox.PushEnabledFrame = -1;
            PushableBox.PullEnabledFrame = -1;
            return;
        }

        // ── Push 模式（张开手掌）────────────────────────────────────────────
        if (currentGesture == GestureType.Push)
        {
            PushableBox.PushEnabledFrame = Time.frameCount;

            if (linkedBox == null)
            {
                // 尝试建立连接
                if (TryLink(BoxLinkMode.Push))
                {
                    // Push：Player 自由移动（moveDirection = 0）
                    // Box 方向过滤在 PushableBox.FixedUpdate 中处理
                    player.moveDirection = 0;
                    player.facingLocked = false; // Push 不锁面朝
                }
            }
        }
        // ── Pull 模式（握拳）──────────────────────────────────────────────
        else if (currentGesture == GestureType.Fist)
        {
            PushableBox.PullEnabledFrame = Time.frameCount;

            if (linkedBox == null)
            {
                // 尝试建立连接
                if (TryLink(BoxLinkMode.Pull))
                {
                    // Pull：锁定面朝方向 + 只能往面朝反方向走
                    player.facingLocked = true;
                    player.moveDirection = player.FacingRight ? -1 : 1;
                }
            }
        }
        // ── 其他手势（无效）→ 断开 ──────────────────────────────────────────
        else
        {
            if (linkedBox != null)
                UnlinkAndClear();
        }

        // 如果已连接但手势帧标记失效（2帧窗口过了），也断开
        if (linkedBox != null && !PushableBox.PushActive && !PushableBox.PullActive)
        {
            UnlinkAndClear();
        }

        // ── 距离检查（替代 IsTouchingPlayer 检查）──────────────────────────
        // 旧的 IsTouchingPlayer 检查是 PULL bug 的根因：
        //   Pull 时 Player 远离 Box → 同帧物理分离 → OnCollisionExit2D
        //   → IsTouchingPlayer = false → 立即断开 → PULL 永远不工作
        //
        // 改用距离检查：只要 Player 和 Box 中心距离不超过 unlinkDistance，
        // 就保持连接。这给 Pull 留了物理移动的空间。
        if (linkedBox != null)
        {
            float dist = Mathf.Abs(linkedBox.transform.position.x - player.transform.position.x);
            if (dist > unlinkDistance)
            {
                UnlinkAndClear();
            }
        }
    }
}
