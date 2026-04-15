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

    [Header("Mirror Zone View")]
    [SerializeField] private bool expandViewInMirrorZone = true;
    [SerializeField, Min(1f)] private float mirrorZoneViewMultiplier = 2f;
    [SerializeField, Min(0f)] private float viewSizeLerpSpeed = 8f;

    private Camera cam;
    private float baseOrthographicSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
            baseOrthographicSize = Mathf.Max(0.01f, cam.orthographicSize);
    }

    void Start()
    {
        UpdateCameraViewSize();

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
        UpdateCameraViewSize();

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
            if (!Application.isPlaying)
                baseOrthographicSize = Mathf.Max(0.01f, cameraComponent.orthographicSize);
        }

        mirrorZoneViewMultiplier = Mathf.Max(1f, mirrorZoneViewMultiplier);
        viewSizeLerpSpeed = Mathf.Max(0f, viewSizeLerpSpeed);
    }

    void UpdateCameraViewSize()
    {
        if (cam == null || !cam.orthographic)
            return;

        float targetSize = baseOrthographicSize;
        if (expandViewInMirrorZone && MirrorController.IsPlayerInAnyMirrorZone)
            targetSize = baseOrthographicSize * mirrorZoneViewMultiplier;

        if (viewSizeLerpSpeed <= 0f)
        {
            cam.orthographicSize = targetSize;
            return;
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, viewSizeLerpSpeed * Time.deltaTime);
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
