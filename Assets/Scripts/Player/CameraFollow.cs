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
    [SerializeField] private bool lockFollowInMirrorZone = true;
    [SerializeField, Min(1f)] private float mirrorZoneViewMultiplier = 2f;
    [SerializeField, Min(0f)] private float viewSizeLerpSpeed = 8f;

    [Header("Mirror Zone Entry Focus")]
    [SerializeField] private bool enableMirrorEntryFocus = true;
    [SerializeField, Min(0f)] private float mirrorEntryFocusHoldTime = 0.25f;
    [SerializeField, Min(0f)] private float mirrorEntryBlendDuration = 0.8f;
    [SerializeField] private AnimationCurve mirrorEntryBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Camera cam;
    private float baseOrthographicSize;
    private bool wasInMirrorZone;
    private float mirrorEntryFocusTimer;
    private float mirrorEntryBlendTimer;

    private enum MirrorEntryFocusState
    {
        None,
        HoldPlayer,
        BlendToMirrorZone,
    }

    private MirrorEntryFocusState mirrorEntryFocusState;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
            baseOrthographicSize = Mathf.Max(0.01f, cam.orthographicSize);
    }

    void Start()
    {
        bool inMirrorZone = TryGetCurrentMirrorFocus(out _);
        UpdateCameraViewSize(inMirrorZone);
        wasInMirrorZone = inMirrorZone;

        if (target == null) return;

        Vector3 followAnchor = GetFollowAnchor(inMirrorZone);
        float targetY = lockY ? fixedY : followAnchor.y + offset.y;
        Vector3 desired = new Vector3(followAnchor.x + offset.x, targetY, transform.position.z);

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
        bool inMirrorZone = TryGetCurrentMirrorFocus(out _);
        UpdateMirrorEntryFocusState(inMirrorZone);
        UpdateCameraViewSize(inMirrorZone);

        if (target == null) return;

        Vector3 followAnchor = GetFollowAnchor(inMirrorZone);
        followAnchor = ApplyMirrorEntryFocusOverride(followAnchor, inMirrorZone);
        float targetY = lockY ? fixedY : followAnchor.y + offset.y;
        Vector3 desired = new Vector3(followAnchor.x + offset.x, targetY, transform.position.z);

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
        mirrorEntryFocusHoldTime = Mathf.Max(0f, mirrorEntryFocusHoldTime);
        mirrorEntryBlendDuration = Mathf.Max(0f, mirrorEntryBlendDuration);
        if (mirrorEntryBlendCurve == null || mirrorEntryBlendCurve.length == 0)
            mirrorEntryBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    void UpdateCameraViewSize(bool inMirrorZone)
    {
        if (cam == null || !cam.orthographic)
            return;

        float targetSize = baseOrthographicSize;
        if (expandViewInMirrorZone && inMirrorZone)
            targetSize = baseOrthographicSize * mirrorZoneViewMultiplier;

        if (viewSizeLerpSpeed <= 0f)
        {
            cam.orthographicSize = targetSize;
            return;
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, viewSizeLerpSpeed * Time.deltaTime);
    }

    Vector3 GetFollowAnchor(bool inMirrorZone)
    {
        if (target == null)
            return transform.position;

        if (lockFollowInMirrorZone && inMirrorZone && MirrorController.TryGetMirrorFocusForPosition(target.position, out Vector3 mirrorFocus))
            return mirrorFocus;

        return target.position;
    }

    bool TryGetCurrentMirrorFocus(out Vector3 mirrorFocus)
    {
        mirrorFocus = Vector3.zero;
        if (target == null)
            return false;

        return MirrorController.TryGetMirrorFocusForPosition(target.position, out mirrorFocus);
    }

    void UpdateMirrorEntryFocusState(bool inMirrorZone)
    {
        if (!enableMirrorEntryFocus)
        {
            mirrorEntryFocusState = MirrorEntryFocusState.None;
            wasInMirrorZone = inMirrorZone;
            return;
        }

        if (inMirrorZone && !wasInMirrorZone)
        {
            mirrorEntryFocusTimer = mirrorEntryFocusHoldTime;
            mirrorEntryBlendTimer = 0f;
            mirrorEntryFocusState = mirrorEntryFocusTimer > 0f
                ? MirrorEntryFocusState.HoldPlayer
                : MirrorEntryFocusState.BlendToMirrorZone;
        }
        else if (!inMirrorZone)
        {
            mirrorEntryFocusState = MirrorEntryFocusState.None;
            mirrorEntryFocusTimer = 0f;
            mirrorEntryBlendTimer = 0f;
        }

        wasInMirrorZone = inMirrorZone;
    }

    Vector3 ApplyMirrorEntryFocusOverride(Vector3 anchor, bool inMirrorZone)
    {
        if (!enableMirrorEntryFocus || !inMirrorZone || target == null)
            return anchor;

        if (mirrorEntryFocusState == MirrorEntryFocusState.HoldPlayer)
        {
            mirrorEntryFocusTimer -= Time.deltaTime;
            if (mirrorEntryFocusTimer > 0f)
                return target.position;

            mirrorEntryFocusState = MirrorEntryFocusState.BlendToMirrorZone;
            mirrorEntryBlendTimer = 0f;
        }

        if (mirrorEntryFocusState == MirrorEntryFocusState.BlendToMirrorZone)
        {
            if (mirrorEntryBlendDuration <= 0f)
            {
                mirrorEntryFocusState = MirrorEntryFocusState.None;
                return anchor;
            }

            mirrorEntryBlendTimer += Time.deltaTime;
            float t = Mathf.Clamp01(mirrorEntryBlendTimer / mirrorEntryBlendDuration);
            float curveT = mirrorEntryBlendCurve != null
                ? Mathf.Clamp01(mirrorEntryBlendCurve.Evaluate(t))
                : t;

            Vector3 blended = Vector3.Lerp(target.position, anchor, curveT);
            if (t >= 1f)
                mirrorEntryFocusState = MirrorEntryFocusState.None;

            return blended;
        }

        return anchor;
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
