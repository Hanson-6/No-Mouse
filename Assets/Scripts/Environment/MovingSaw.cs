using UnityEngine;

/// <summary>
/// 在两个端点之间来回移动的锯子。
/// 触碰玩家即触发死亡。
/// </summary>
public class MovingSaw : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 pointA;
    public Vector2 pointB;
    public float speed = 3f;

    [Header("Rotation")]
    public float rotationSpeed = 360f;

    private bool goingToB = true;

    void Start()
    {
        if (pointA == Vector2.zero) pointA = transform.position;
        if (pointB == Vector2.zero) pointB = (Vector2)transform.position + Vector2.right * 4f;
    }

    void Update()
    {
        Vector2 target = goingToB ? pointB : pointA;
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target) < 0.05f)
            goingToB = !goingToB;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponent<PlayerController>()?.Die();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(pointA, 0.1f);
        Gizmos.DrawSphere(pointB, 0.1f);
        Gizmos.DrawLine(pointA, pointB);
    }
}
