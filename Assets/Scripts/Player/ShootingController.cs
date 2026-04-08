using UnityEngine;
using GestureRecognition.Core;

/// <summary>
/// 挂在 Player 上。检测到 Shoot 手势时，按全局冷却时间发射子弹。
///
/// 射击冷却逻辑（全局冷却，不因手势取消而重置）：
///   第一次检测到 Shoot 手势 → 立即开枪 → 开始 cooldown 计时
///   cooldown 期间：不管用户做什么（保持手势 / 取消 / 再做 Shoot），都不能开枪
///   cooldown 结束后：检查当前帧是否是 Shoot 手势
///     → 是 → 开枪 + 重新 cooldown
///     → 不是 → 不开枪，等下次 Shoot
///
/// 这样做的好处：
///   防止玩家通过快速切换手势来绕过冷却时间实现高速连射。
///
/// FirePoint 镜像：
///   FirePoint 是 Player 的子物体，localPosition.x = 0.3（在 Player 右侧）。
///   Player 面朝左时，脚本将 FirePoint.localPosition.x 翻转为 -0.3，
///   确保子弹总是从 Player 面朝方向的前方生成。
/// </summary>
public class ShootingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;   // 子弹生成位置（挂在 Player 上的子物体）

    [Header("Settings")]
    [SerializeField] private float fireRate = 0.4f; // 射击冷却时间（秒）

    [Header("Audio")]
    [SerializeField] private AudioClip shootSound;  // 射击音效（暂未赋值，留好接口）

    // isShooting: 当前帧是否检测到 Shoot 手势
    private bool isShooting = false;

    // nextFireTime: 全局冷却计时器。
    // 关键：这个值 **不会** 在手势取消时重置。
    // 即使用户中途取消手势再重新做 Shoot，也必须等 cooldown 结束。
    private float nextFireTime = 0f;

    // firePointLocalX: FirePoint 的初始 localPosition.x（正值）
    // 用于根据 Player 面朝方向镜像 FirePoint 位置
    private float firePointLocalX;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (player == null) player = GetComponent<PlayerController>();

        // 记录 FirePoint 的初始 x 偏移（绝对值），用于后续镜像
        if (firePoint != null)
            firePointLocalX = Mathf.Abs(firePoint.localPosition.x);
    }

    void OnEnable()
    {
        GestureEvents.OnGestureUpdated += OnGesture;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGesture;
        isShooting = false;
        // 注意：不重置 nextFireTime！
        // 全局冷却在 OnDisable 时也保持，防止场景重载时绕过冷却。
    }

    void OnGesture(GestureResult result)
    {
        isShooting = result.Type == GestureType.Shoot;
    }

    void Update()
    {
        // 每帧更新 FirePoint 位置，使其始终在 Player 面朝方向的前方
        if (firePoint != null && player != null)
        {
            Vector3 localPos = firePoint.localPosition;
            localPos.x = player.FacingRight ? firePointLocalX : -firePointLocalX;
            firePoint.localPosition = localPos;
        }

        // 冷却检查：全局冷却未结束，不管手势状态如何都不开枪
        if (Time.time < nextFireTime) return;

        // 手势检查：冷却结束后，当前帧是 Shoot 手势才开枪
        if (!isShooting) return;

        if (bulletPrefab == null || firePoint == null) return;

        Fire();
        nextFireTime = Time.time + fireRate;
    }

    void Fire()
    {
        float dir = player.FacingRight ? 1f : -1f;

        var go = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        var bullet = go.GetComponent<Bullet>();
        if (bullet != null) bullet.Init(dir);

        // 射击音效（shootSound 暂未赋值，队友后续添加音效文件后
        // 只需在 Inspector 中拖入 AudioClip 即可，代码无需修改）
        if (audioSource != null && shootSound != null)
            audioSource.PlayOneShot(shootSound);
    }
}
