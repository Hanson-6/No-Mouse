using UnityEngine;

/// <summary>
/// 静态尖刺 — 触碰即触发玩家死亡。
/// 挂在带 Trigger Collider2D 的 GameObject 上。
/// </summary>
public class Spike : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponent<PlayerController>()?.Die();
    }
}
