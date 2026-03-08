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

    private bool goingToB = true;
    private Vector2 lastPos;

    void Start()
    {
        pointA = (Vector2)transform.position;
        pointB = pointA + Vector2.right * 5f;
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
