using UnityEngine;
using GestureRecognition.Core;

// Push（张开手掌）：玩家只能往面朝方向推箱子
// Pull（握拳）    ：玩家只能往面朝反方向拉箱子
//
// 触发条件（Push 和 Pull 相同，3 个都要满足）：
//   1. 玩家做出对应手势
//   2. 玩家与箱子发生水平方向碰撞（不是从上面跳上去的）
//   3. 玩家面朝箱子（spriteRenderer.flipX 与 Box 相对位置一致）
//
// 连接建立后：
//   Player 和 Box 共享水平速度（紧贴移动）
//   Push 时 Player 只能往面朝方向走（反方向按键无效）
//   Pull 时 Player 只能往面朝反方向走，且面朝方向锁定
//
// 断开条件：
//   手势消失（帧级别判断）→ Unlink + 解除所有锁定
public class GestureInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    // 当前手势类型
    private GestureType currentGesture = GestureType.None;

    // 当前已连接的箱子（Push 或 Pull 进行中）
    private PushableBox linkedBox;

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
    bool TryLink()
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

            box.Link();
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

        // ── Push 模式（张开手掌）────────────────────────────────────────────
        if (currentGesture == GestureType.Push)
        {
            PushableBox.PushEnabledFrame = Time.frameCount;

            if (linkedBox == null)
            {
                // 尝试建立连接
                if (TryLink())
                {
                    // Push：只能往面朝方向走
                    player.moveDirection = player.FacingRight ? 1 : -1;
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
                if (TryLink())
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
    }
}
