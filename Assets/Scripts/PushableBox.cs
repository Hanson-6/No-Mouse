using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PushableBox : MonoBehaviour
{
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
    private Rigidbody2D playerRb;   // 当前接触箱子的玩家的 Rigidbody2D（碰撞检测时获取）

    // ── 连接状态 ────────────────────────────────────────────────────────────
    // isLinked: 碰撞触发后由 GestureInputBridge 设为 true，手势消失时设为 false
    // 不依赖 OnCollisionExit2D 断开——因为 Push/Pull 时 Player 和 Box 始终紧贴
    private bool isLinked;

    // horizontalTouch: 水平方向碰撞标记，区分"从侧面碰"和"从上面跳上去"
    private bool horizontalTouch;

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

        // 如果已经 Link（Push/Pull 进行中），不清空 playerRb
        // 因为 Push/Pull 时 Player 和 Box 紧贴，共享速度
        // 断开只在手势消失时由 GestureInputBridge 调用 Unlink()
        if (!isLinked)
        {
            playerRb = null;
            horizontalTouch = false;
        }
    }
    // 总结：
    // - 玩家离开箱子时，如果没有 Link，清空引用
    // - 如果已 Link（正在 Push/Pull），保持引用不清空


    /// <summary>
    /// GestureInputBridge 调用：建立 Push/Pull 连接。
    /// 连接后 Box 跟随 Player 水平速度，不受碰撞 Exit 影响。
    /// </summary>
    public void Link()
    {
        isLinked = true;
    }

    /// <summary>
    /// GestureInputBridge 调用：断开 Push/Pull 连接。
    /// 断开后 Box 停止移动，等待下一次碰撞触发。
    /// </summary>
    public void Unlink()
    {
        isLinked = false;
        playerRb = null;
        horizontalTouch = false;
        rb.velocity = new Vector2(0f, rb.velocity.y); // 断开时停止水平移动
    }

    /* Unity 内置函数 */
    // - FixedUpdate 每个物理帧调用一次（默认每秒 50 次，时间间隔 0.02 秒）
    // - 因为物理引擎要求稳定的时间步长，所以 Unity 把物理逻辑放在 FixedUpdate。
    //
    // 设计思路（Push 和 Pull 对称）：
    // - 触发条件（3个）：手势激活 + 水平碰撞 + 玩家面朝 Box（由 GestureInputBridge 判断）
    // - 连接建立后：Box 跟随 Player 水平速度
    // - Push（张开手掌）：Player 只能往面朝方向移动
    // - Pull（握拳）    ：Player 只能往面朝反方向移动，且面朝方向锁定
    // - 断开条件：手势消失（由 GestureInputBridge 调用 Unlink）
    void FixedUpdate()
    {
        if (isLinked && playerRb != null)
        {
            // rb.constraints 控制刚体的运动约束——冻结哪些轴的运动或旋转。
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 只冻结旋转
            rb.velocity = new Vector2(playerRb.velocity.x, rb.velocity.y); // 水平速度跟随玩家，垂直速度保持不变
        }
        else
        {
            rb.constraints = RigidbodyConstraints2D.FreezePosition | RigidbodyConstraints2D.FreezeRotation; // 冻结 XY 移动 AND 冻结旋转
        }
    }

    public Rigidbody2D Rb => rb;

    /// <summary>玩家是否正在水平方向物理接触这个箱子</summary>
    public bool IsTouchingPlayer => horizontalTouch && playerRb != null;

    /// <summary>是否已建立 Push/Pull 连接</summary>
    public bool IsLinked => isLinked;
}
