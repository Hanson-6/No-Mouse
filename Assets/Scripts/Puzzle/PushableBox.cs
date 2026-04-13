using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 连接模式：None = 未连接, Push = 推, Pull = 拉。
/// GestureInputBridge 调用 Link(mode) 时传入。
/// </summary>
public enum BoxLinkMode { None, Push, Pull }

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PushableBox : MonoBehaviour, ISnapshotSaveable
{
    // ── Step Climb ──────────────────────────────────────────────────────────
    [Header("Step Climb")]
    [Tooltip("Max height the box can automatically step up (match or slightly exceed the platform-to-terrain height difference)")]
    [SerializeField] private float maxStepHeight = 0.465f;
    [Tooltip("Horizontal distance to probe ahead for steps")]
    [SerializeField] private float stepCheckDistance = 0.15f;
    [Tooltip("Edge radius on the BoxCollider2D — rounds corners to slide over tiny height differences")]
    [SerializeField] private float colliderEdgeRadius = 0.02f;
    [Tooltip("Layer mask for ground/terrain (must match PlayerController's groundLayer)")]
    [SerializeField] private LayerMask groundLayer;
    // ── 手势激活帧 ──────────────────────────────────────────────────────────
    // GestureInputBridge 每帧刷新；超过1帧未刷新自动失效
    public static int PushEnabledFrame = -1; // 判断"推箱子手势"是否有效，存储的是一个帧编号
    public static bool PushActive => PushEnabledFrame >= Time.frameCount - 1;
    // public 公开访问，任何地方都能读
    // static 属于类本身，而非实例
    // bool 返回布尔值
    // PushActive 属性名（"推是否激活"）
    // => 等同于 return

    // PushEnabledFrame 手势系统写入的"帧号"，比如当前是第 100 帧
    // Time.frameCount 是 Unity 内置的当前帧数（从游戏开始时计数：1, 2，3，...）
    // 因此，这行代码的意思是 "推箱子是否有效" = 当前帧 是否 小于等于 PushEnabledFrame + 1

    // Pull 也用同样的帧检测机制
    public static int PullEnabledFrame = -1;
    public static bool PullActive => PullEnabledFrame >= Time.frameCount - 1;

    /* 静态注册列表，用于管理所有箱子实例 */
    private static readonly List<PushableBox> _allBoxes = new List<PushableBox>();
    // 私有：真正的数据存储
    public static IReadOnlyList<PushableBox> AllBoxes => _allBoxes;
    // 公开只读：外部只能读，不能修改 _allBoxes 列表

    // 因此这两行代码的作用是：
    // - 箱子启用（放入场景），触发：onEnable() -> 加入 _allBoxes 列表
    // - 箱子禁用（移出场景），触发：onDisable() -> 从 _allBoxes 列表移除

    // 不用每次都 FindObjectsOfType<PushableBox>() -> 低效：每帧搜索场景中所有对象

    private Rigidbody2D rb; // 箱子自己的 Rigidbody2D（通过 GetComponent 获取）
    private BoxCollider2D boxCollider; // 用于 step climb 检测
    private Rigidbody2D playerRb;   // 当前接触箱子的玩家的 Rigidbody2D（碰撞检测时获取）

    // ── 连接状态 ────────────────────────────────────────────────────────────
    // isLinked: 碰撞触发后由 GestureInputBridge 设为 true，手势消失时设为 false
    // 不依赖 OnCollisionExit2D 断开——因为 Push/Pull 时 Player 和 Box 始终紧贴
    private bool isLinked;

    // linkMode: 当前连接模式（Push 或 Pull）
    // Push 模式：Box 只在 Player 往推方向移动时跟随，反方向移动时 Box 不动
    // Pull 模式：Box 完全跟随 Player 水平速度
    private BoxLinkMode linkMode = BoxLinkMode.None;

    // pushDirection: Push 模式下 Box 被推的方向（+1 = 往右推, -1 = 往左推）
    // 由 Link() 时根据 Player 面朝方向确定：面朝右 → pushDirection = +1（Box 在右边，往右推）
    private float pushDirection;

    // horizontalTouch: 水平方向碰撞标记，区分"从侧面碰"和"从上面跳上去"
    private bool horizontalTouch;

    // ── MovingPlatform 跟随 ─────────────────────────────────────────────────
    // 当 Box 站在 MovingPlatform 上但未被 Link 时，需要跟随平台水平移动
    private MovingPlatform currentPlatform;

    [System.Serializable]
    private class SnapshotState
    {
        public bool activeSelf;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float velocityX;
        public float velocityY;
        public bool isLinked;
        public int linkMode;
        public float pushDirection;
        public bool horizontalTouch;
    }

    /* Unity 内置函数 */
    // - Awake 在脚本实例创建时（最早）时调用
    // 因此，就是在被实例化时，做初始化赋值

    // 当 PushableBox 被挂到场景里的某个 GameObject 时，Unity 会自动
    // - 创建这个脚本的实例
    // - 调用 Awake()
    // - 执行里面的代码（获取自己身上的 Rigidbody2D）
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Round the bottom corners of the BoxCollider2D so the box
        // can slide over tiny height differences naturally.
        if (boxCollider != null && colliderEdgeRadius > 0f)
        {
            boxCollider.edgeRadius = colliderEdgeRadius;
        }

        // Auto-detect groundLayer from PlayerController if not assigned
        if (groundLayer == 0)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
                groundLayer = player.groundLayer;
        }
    }

    void OnEnable()  => _allBoxes.Add(this);
    void OnDisable() => _allBoxes.Remove(this);

    // Collision2D col 是 Unity 会自动传入"碰撞数据对象"，包含：
    // - col.gameObject：碰撞的另一个对象（比如玩家）
    // - col.contacts[0].normal：碰撞法线，用来判断碰撞方向
    void OnCollisionEnter2D(Collision2D col)
    {
        // CompareTag("Player") --> 判断标签是否为 "Player"（推荐，比 == 快）
        if (!col.gameObject.CompareTag("Player")) return;

        // 检查碰撞法线：水平碰撞才算有效接触
        // normal.x 的绝对值 > normal.y 的绝对值 → 碰撞方向偏水平
        // 例：玩家从左边碰 Box → normal = (1, 0)（水平）
        //     玩家从上面跳到 Box → normal = (0, 1)（垂直）→ 不算有效
        if (col.contactCount > 0)
        {
            Vector2 normal = col.contacts[0].normal;
            if (Mathf.Abs(normal.x) <= Mathf.Abs(normal.y)) return; // 垂直碰撞，忽略
        }

        playerRb = col.gameObject.GetComponent<Rigidbody2D>();
        horizontalTouch = true;
    }
    // 总结：
    // - Unity 检测当前碰撞 Box 的是否是 Player
    // - 必须是水平方向碰撞（不是从上面跳上去的）
    // - 如果满足条件，存储 Player 的 RB，标记为水平接触


    /* Unity 内置函数 */
    // - OnCollisionExit2D 当碰撞体退出时调用
    void OnCollisionExit2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Player")) return;

        // 无论是否 isLinked，都清空 horizontalTouch。
        // 这表示 Player 和 Box 在物理上已经分离了。
        //
        // 如果是 Push/Pull 进行中 Box 掉进洞里等情况，
        // horizontalTouch = false 会让 IsTouchingPlayer = false，
        // GestureInputBridge 在距离检查中会自动断开连接。
        //
        // 如果是手势切换（Push→Pull），Player 和 Box 仍然紧贴，
        // Unity 不会触发 OnCollisionExit2D，所以 horizontalTouch 保持 true，
        // 无缝切换不受影响。
        horizontalTouch = false;
        if (!isLinked)
        {
            playerRb = null;
        }
    }
    // 总结：
    // - 玩家物理离开箱子时，horizontalTouch 一定清空
    // - 如果没有 Link，同时清空 playerRb
    // - 如果已 Link（正在 Push/Pull），保留 playerRb（让 FixedUpdate 继续同步速度直到 GestureInputBridge 断开）


    /// <summary>
    /// GestureInputBridge 调用：建立 Push/Pull 连接。
    /// 连接后 Box 跟随 Player 水平速度（Push 模式会过滤方向）。
    ///
    /// mode: Push 或 Pull
    /// playerFacingRight: 建立连接时 Player 的面朝方向
    ///   Push 时 pushDirection = 面朝方向（Box 在 Player 面朝那一侧）
    /// </summary>
    public void Link(BoxLinkMode mode, bool playerFacingRight)
    {
        isLinked = true;
        linkMode = mode;
        // Push 时记录推的方向：Player 面朝右 → Box 在右边 → 推方向 = +1
        pushDirection = playerFacingRight ? 1f : -1f;
    }

    /// <summary>
    /// GestureInputBridge 调用：断开 Push/Pull 连接。
    /// 断开后 Box 停止移动，等待下一次碰撞触发。
    /// </summary>
    public void Unlink()
    {
        isLinked = false;
        linkMode = BoxLinkMode.None;
        // 不清 playerRb 和 horizontalTouch！
        // 如果 Player 仍然贴着 Box，这两个值应该保持 true，
        // 让下一次 TryLink()（手势切换时）能立即重新连接。
        // Player 真正离开时 OnCollisionExit2D 会清空 horizontalTouch，
        // GestureInputBridge 距离检查超限后断开连接。
        rb.velocity = new Vector2(0f, rb.velocity.y); // 断开时停止水平移动
    }

    /// <summary>
    /// MovingPlatform 碰撞时调用：记录当前所在平台，使 Box 跟随平台水平移动。
    /// </summary>
    public void SetCurrentPlatform(MovingPlatform platform)
    {
        currentPlatform = platform;
    }

    /// <summary>
    /// 离开 MovingPlatform 时调用：清除平台引用。
    /// </summary>
    public void ClearCurrentPlatform(MovingPlatform platform)
    {
        if (currentPlatform == platform)
            currentPlatform = null;
    }

    /* Unity 内置函数 */
    // - FixedUpdate 每个物理帧调用一次（默认每秒 50 次，时间间隔 0.02 秒）
    // - 因为物理引擎要求稳定的时间步长，所以 Unity 把物理逻辑放在 FixedUpdate。
    //
    // 设计思路（Push 和 Pull 不同）：
    // - 触发条件（3个）：手势激活 + 水平碰撞 + 玩家面朝 Box（由 GestureInputBridge 判断）
    // - 连接建立后：
    //   Push 模式：只有当 Player 往推方向移动时，Box 才跟随移动
    //             Player 反方向移动时，Box 水平速度 = 0（不动）
    //   Pull 模式：Box 完全跟随 Player 水平速度
    // - 断开条件：手势消失 / 距离超限（由 GestureInputBridge 调用 Unlink）
    void FixedUpdate()
    {
        if (isLinked && playerRb != null)
        {
            // ── 已连接：Dynamic 模式，响应 Player 的推/拉 ──────────────────
            // 确保是 Dynamic（从 Kinematic 切换回来时需要）
            if (rb.bodyType != RigidbodyType2D.Dynamic)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.useFullKinematicContacts = false;
            }

            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            float playerVx = playerRb.velocity.x;

            if (linkMode == BoxLinkMode.Push)
            {
                bool movingInPushDir = (pushDirection > 0f && playerVx > 0f)
                                    || (pushDirection < 0f && playerVx < 0f);
                float boxVx = movingInPushDir ? playerVx : 0f;

                // 在平台上时叠加平台速度，保证推动过程中 Box 不会"掉队"
                if (currentPlatform != null)
                    boxVx += currentPlatform.CurrentVelocityX;

                rb.velocity = new Vector2(boxVx, rb.velocity.y);
            }
            else // Pull 模式
            {
                float boxVx = playerVx;
                if (currentPlatform != null)
                    boxVx += currentPlatform.CurrentVelocityX;
                rb.velocity = new Vector2(boxVx, rb.velocity.y);
            }

            // Step-up: auto-climb small ledges when the box is moving horizontally
            if (Mathf.Abs(rb.velocity.x) > 0.01f)
            {
                Vector2 dir = rb.velocity.x > 0f ? Vector2.right : Vector2.left;
                TryStepUp(dir);
            }
        }
        else
        {
            // ── 未连接状态 ────────────────────────────────────────────────
            if (currentPlatform != null)
            {
                // 在 MovingPlatform 上：切换为 Kinematic
                // Kinematic 刚体不会被 Player 的碰撞力推动，
                // 但仍然会阻挡 Player（Player 是 Dynamic，撞上来会被挡住）。
                // 用 MovePosition 跟随平台移动。
                if (rb.bodyType != RigidbodyType2D.Kinematic)
                {
                    rb.velocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    // Must enable full kinematic contacts so that
                    // OnCollisionExit2D still fires between this
                    // Kinematic box and the Static platform collider
                    // (MovingPlatform has no Rigidbody2D).
                    rb.useFullKinematicContacts = true;
                }

                float platformDeltaX = currentPlatform.CurrentVelocityX * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + new Vector2(platformDeltaX, 0f));
            }
            else
            {
                // 不在平台上：切换回 Dynamic，冻结 X（只靠重力下落）
                if (rb.bodyType != RigidbodyType2D.Kinematic)
                {
                    // 已经是 Dynamic，正常设约束
                    rb.constraints = RigidbodyConstraints2D.FreezePositionX
                                   | RigidbodyConstraints2D.FreezeRotation;
                }
                else
                {
                    // 从 Kinematic 切回 Dynamic（刚离开平台）
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.useFullKinematicContacts = false;
                    rb.constraints = RigidbodyConstraints2D.FreezePositionX
                                   | RigidbodyConstraints2D.FreezeRotation;
                }
            }
        }
    }

    /// <summary>
    /// 检测箱子前方是否有可以自动爬上的小台阶，逻辑与 PlayerController.TryStepUp 一致。
    /// 当箱子在 MovingPlatform 上被推向地形边缘时，能自动翻越微小的高度差。
    /// </summary>
    void TryStepUp(Vector2 direction)
    {
        if (boxCollider == null || maxStepHeight <= 0f) return;

        Bounds bounds = boxCollider.bounds;

        float halfWidth = bounds.extents.x;
        float rayOriginX = bounds.center.x;
        float totalRayDist = halfWidth + stepCheckDistance;

        // 1. Foot-level ray — is there a ledge ahead?
        Vector2 footOrigin = new Vector2(rayOriginX, bounds.min.y + 0.02f);
        RaycastHit2D lowHit = Physics2D.Raycast(footOrigin, direction, totalRayDist, groundLayer);
        if (lowHit.collider == null) return;

        float wallX = lowHit.point.x;
        float leadingX = direction.x > 0f ? bounds.max.x : bounds.min.x;

        // 2. High ray — if clear, it's a step, not a wall
        Vector2 highOrigin = new Vector2(rayOriginX, bounds.min.y + maxStepHeight);
        RaycastHit2D highHit = Physics2D.Raycast(highOrigin, direction, totalRayDist, groundLayer);
        if (highHit.collider != null) return; // 前方是一面完整的墙，不是台阶

        // 3. Downward ray to find step surface
        float distToWall = Mathf.Abs(wallX - leadingX);
        float probePastWall = distToWall + 0.08f;
        Vector2 overStepOrigin = new Vector2(
            leadingX + direction.x * probePastWall,
            bounds.min.y + maxStepHeight + 0.02f);
        RaycastHit2D downHit = Physics2D.Raycast(overStepOrigin, Vector2.down, maxStepHeight + 0.04f, groundLayer);
        if (downHit.collider == null) return;

        float heightDiff = downHit.point.y - bounds.min.y;
        if (heightDiff <= 0.005f || heightDiff > maxStepHeight) return;

        // 4. Teleport UP + FORWARD
        float forwardNudge = probePastWall + 0.02f;
        Vector2 newPos = rb.position;
        newPos.y += heightDiff + 0.02f;
        newPos.x += direction.x * forwardNudge;
        rb.position = newPos;

        if (rb.velocity.y < 0f)
            rb.velocity = new Vector2(rb.velocity.x, 0f);
    }

    public Rigidbody2D Rb => rb;

    /// <summary>玩家是否正在水平方向物理接触这个箱子</summary>
    public bool IsTouchingPlayer => horizontalTouch && playerRb != null;

    /// <summary>是否已建立 Push/Pull 连接</summary>
    public bool IsLinked => isLinked;

    /// <summary>当前连接模式</summary>
    public BoxLinkMode LinkMode => linkMode;

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            activeSelf = gameObject.activeSelf,
            positionX = transform.position.x,
            positionY = transform.position.y,
            positionZ = transform.position.z,
            velocityX = rb != null ? rb.velocity.x : 0f,
            velocityY = rb != null ? rb.velocity.y : 0f,
            isLinked = isLinked,
            linkMode = (int)linkMode,
            pushDirection = pushDirection,
            horizontalTouch = horizontalTouch
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        gameObject.SetActive(snapshot.activeSelf);
        transform.position = new Vector3(snapshot.positionX, snapshot.positionY, snapshot.positionZ);

        if (rb != null)
        {
            // Reset to Dynamic so FixedUpdate state machine starts clean.
            // If the box was Kinematic (on a platform) before snapshot,
            // it will re-enter Kinematic naturally once OnCollisionEnter2D
            // re-fires from the platform.
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.useFullKinematicContacts = false;
            rb.velocity = new Vector2(snapshot.velocityX, snapshot.velocityY);
        }

        // Clear platform reference — will be re-established by collision events
        currentPlatform = null;

        isLinked = snapshot.isLinked;
        int maxMode = (int)BoxLinkMode.Pull;
        linkMode = (BoxLinkMode)Mathf.Clamp(snapshot.linkMode, 0, maxMode);
        pushDirection = snapshot.pushDirection;
        horizontalTouch = snapshot.horizontalTouch;

        if (!isLinked)
            playerRb = null;
    }
}
