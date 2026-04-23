using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DarkVisionLamp : MonoBehaviour
{
    private static readonly List<DarkVisionLamp> activeLamps = new List<DarkVisionLamp>(16);

    [Header("Vision")]
    [SerializeField, Min(0.05f)] private float visibleRadius = 2f;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.2f;
    [SerializeField] private Vector2 focusOffsetWorld = Vector2.zero;
    [SerializeField] private bool useColliderCenter = true;

    private Collider2D cachedCollider;

    public static IReadOnlyList<DarkVisionLamp> ActiveLamps => activeLamps;
    public float VisibleRadius => Mathf.Max(0.05f, visibleRadius);
    public float EdgeSoftness => Mathf.Max(0f, edgeSoftness);

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        Register(this);
    }

    void OnDisable()
    {
        Unregister(this);
    }

    void OnDestroy()
    {
        Unregister(this);
    }

    void OnValidate()
    {
        visibleRadius = Mathf.Max(0.05f, visibleRadius);
        edgeSoftness = Mathf.Max(0f, edgeSoftness);
    }

    public Vector2 GetWorldCenter()
    {
        if (useColliderCenter && cachedCollider != null)
            return (Vector2)cachedCollider.bounds.center + focusOffsetWorld;

        return (Vector2)transform.position + focusOffsetWorld;
    }

    private static void Register(DarkVisionLamp lamp)
    {
        if (lamp == null)
            return;

        if (!activeLamps.Contains(lamp))
            activeLamps.Add(lamp);
    }

    private static void Unregister(DarkVisionLamp lamp)
    {
        if (lamp == null)
            return;

        activeLamps.Remove(lamp);
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? GetWorldCenter() : (Vector2)transform.position + focusOffsetWorld;
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(center, VisibleRadius);
    }
}
