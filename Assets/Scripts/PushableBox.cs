using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PushableBox : MonoBehaviour
{
    // GestureInputBridge 每帧刷新；超过1帧未刷新自动失效
    public static int PushEnabledFrame = -1;
    public static bool PushActive => PushEnabledFrame >= Time.frameCount - 1;

    private Rigidbody2D rb;
    private Rigidbody2D playerRb;   // 玩家正在接触时有值
    private bool isGrabbed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            playerRb = col.gameObject.GetComponent<Rigidbody2D>();
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            playerRb = null;
    }

    void FixedUpdate()
    {
        if (isGrabbed) return;

        if (PushActive && playerRb != null)
        {
            // 解冻 X 轴，同步玩家水平速度
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.velocity = new Vector2(playerRb.velocity.x, rb.velocity.y);
        }
        else
        {
            // 冻结 X 轴：物理引擎级别的锁定，碰撞冲量也无法移动
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public Rigidbody2D Rb => rb;

    public void Grab()
    {
        isGrabbed = true;
        rb.velocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.isKinematic = true;
    }

    public void Release()
    {
        isGrabbed = false;
        rb.isKinematic = false;
        rb.velocity = Vector2.zero;
        // 恢复默认冻结（等待下一帧 FixedUpdate 根据 PushActive 更新）
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }
}
