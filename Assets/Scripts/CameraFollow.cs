using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector2 offset = new Vector2(0f, 2f);

    [Header("Lock Y Position")]
    public bool lockY = false;
    public float fixedY = 0f;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    void Start()
    {
        if (target == null) return;

        float targetY = lockY ? fixedY : target.position.y + offset.y;
        Vector3 desired = new Vector3(target.position.x + offset.x, targetY, transform.position.z);

        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
        }

        transform.position = desired;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float targetY = lockY ? fixedY : target.position.y + offset.y;
        Vector3 desired = new Vector3(target.position.x + offset.x, targetY, transform.position.z);

        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
        }

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
