using UnityEngine;

[DefaultExecutionOrder(-10)]   // 确保在 ParallaxLayer 之前执行
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector2 offset = new Vector2(0f, 2f);

    [Header("Pixel Snap")]
    [SerializeField] private bool snapToPixelGrid = true;

    [Header("Lock Y Position")]
    public bool lockY = false;
    public float fixedY = 0f;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

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

        desired = SnapToPixelGrid(desired);

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

        Vector3 smoothed = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.position = SnapToPixelGrid(smoothed);
    }

    void OnValidate()
    {
        var cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.allowMSAA = false;
        }
    }

    Vector3 SnapToPixelGrid(Vector3 worldPosition)
    {
        if (!snapToPixelGrid || cam == null || !cam.orthographic || Screen.height <= 0)
            return worldPosition;

        float unitsPerPixel = (cam.orthographicSize * 2f) / Screen.height;
        worldPosition.x = Mathf.Round(worldPosition.x / unitsPerPixel) * unitsPerPixel;
        worldPosition.y = Mathf.Round(worldPosition.y / unitsPerPixel) * unitsPerPixel;
        return worldPosition;
    }
}
