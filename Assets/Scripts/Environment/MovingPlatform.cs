using UnityEngine;

/// <summary>
/// 在两个端点之间来回移动的平台。
/// 玩家站上去会随平台移动。
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 pointA;
    public Vector2 pointB;
    public float speed = 2f;
    [Tooltip("If true, platform starts at Point B and moves toward Point A first")]
    public bool startAtPointB = false;

    private bool goingToB;
    private Vector2 lastPos;

    void Start()
    {
        // Only default to transform position if points were not set in the Inspector
        if (pointA == Vector2.zero && pointB == Vector2.zero)
        {
            pointA = (Vector2)transform.position;
            pointB = pointA + Vector2.right * 5f;
        }
        goingToB = !startAtPointB;
        if (startAtPointB)
            transform.position = pointB;
        lastPos = transform.position;
    }

    void Update()
    {
        Vector2 target = goingToB ? pointB : pointA;
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target) < 0.05f)
            goingToB = !goingToB;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(transform);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(null);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pointA == Vector2.zero ? (Vector2)transform.position : pointA, 0.1f);
        Gizmos.DrawSphere(pointB == Vector2.zero ? (Vector2)transform.position + Vector2.right * 5f : pointB, 0.1f);
        Gizmos.DrawLine(
            pointA == Vector2.zero ? (Vector2)transform.position : pointA,
            pointB == Vector2.zero ? (Vector2)transform.position + Vector2.right * 5f : pointB);
    }
}
