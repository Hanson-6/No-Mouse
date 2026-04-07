using UnityEngine;
using GestureRecognition.Core;

/// <summary>
/// 挂在 Player 上。检测到 Shoot 手势时，按 fireRate 频率发射子弹。
/// </summary>
public class ShootingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;   // 子弹生成位置（挂在 Player 上的子物体）

    [Header("Settings")]
    [SerializeField] private float fireRate = 0.4f; // 每秒最多发射次数的间隔（秒）

    [Header("Audio")]
    [SerializeField] private AudioClip shootSound;

    private bool isShooting = false;
    private float nextFireTime = 0f;
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (player == null) player = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        GestureEvents.OnGestureUpdated += OnGesture;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGesture;
        isShooting = false;
    }

    void OnGesture(GestureResult result)
    {
        isShooting = result.Type == GestureType.Shoot;
    }

    void Update()
    {
        if (!isShooting) return;
        if (Time.time < nextFireTime) return;
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

        if (audioSource != null && shootSound != null)
            audioSource.PlayOneShot(shootSound);
    }
}
