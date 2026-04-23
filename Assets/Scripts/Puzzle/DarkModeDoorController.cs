using UnityEngine;

[DisallowMultipleComponent]
public sealed class DarkModeDoorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SwitchDoor targetDoor;

    [Header("Behavior")]
    [SerializeField] private bool openWhenDark = true;
    [SerializeField] private bool closeAfterPlayerPass = true;

    [Header("Pass Detection")]
    [SerializeField] private Vector2 passZoneCenterOffset = new Vector2(0f, 0.5f);
    [SerializeField, Min(0.1f)] private float passZoneHalfWidth = 0.9f;
    [SerializeField, Min(0.1f)] private float passZoneHalfHeight = 1.6f;
    [SerializeField, Min(0f)] private float sideEpsilon = 0.05f;

    private bool hasAppliedState;
    private bool hasDarkState;
    private bool lastDarkMode;
    private bool lastShouldOpen;
    private bool closedAfterPass;

    private bool hasPassZoneOrigin;
    private Vector2 passZoneOriginWorld;

    private Transform playerTransform;
    private bool trackingPass;
    private int trackedEntrySide;

    private void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponent<SwitchDoor>();

        CachePassZoneOrigin();
    }

    private void OnEnable()
    {
        ResetPassTracking();
        ApplyDarkModeState(force: true);
    }

    private void Update()
    {
        ApplyDarkModeState(force: false);
        DetectPlayerPass();
    }

    private void ApplyDarkModeState(bool force)
    {
        if (targetDoor == null)
            return;

        bool isDarkMode = GameData.IsDarkModeActive;
        if (!hasDarkState || isDarkMode != lastDarkMode)
        {
            if (!isDarkMode)
            {
                closedAfterPass = false;
                ResetPassTracking();
            }

            hasDarkState = true;
            lastDarkMode = isDarkMode;
        }

        bool shouldOpen = ShouldDoorOpen(isDarkMode);
        if (!force && hasAppliedState && shouldOpen == lastShouldOpen)
            return;

        if (shouldOpen)
            targetDoor.Open();
        else
            targetDoor.Close();

        lastShouldOpen = shouldOpen;
        hasAppliedState = true;
    }

    public void NotifyPlayerPassed()
    {
        if (!closeAfterPlayerPass || targetDoor == null || closedAfterPass)
            return;

        bool isDarkMode = GameData.IsDarkModeActive;
        bool openByDarkMode = openWhenDark ? isDarkMode : !isDarkMode;
        if (!openByDarkMode)
            return;

        closedAfterPass = true;
        targetDoor.Close();
        lastShouldOpen = false;
        hasAppliedState = true;
        ResetPassTracking();
    }

    private bool ShouldDoorOpen(bool isDarkMode)
    {
        bool openByDarkMode = openWhenDark ? isDarkMode : !isDarkMode;
        if (!openByDarkMode)
            return false;

        return !closeAfterPlayerPass || !closedAfterPass;
    }

    private void DetectPlayerPass()
    {
        if (!closeAfterPlayerPass || closedAfterPass)
            return;

        bool isDarkMode = GameData.IsDarkModeActive;
        if (!ShouldDoorOpen(isDarkMode))
            return;

        ResolvePlayer();
        if (playerTransform == null)
            return;

        CachePassZoneOrigin();
        Vector2 zoneCenter = passZoneOriginWorld + passZoneCenterOffset;
        Vector2 playerPos = playerTransform.position;
        Vector2 delta = playerPos - zoneCenter;

        bool insideZone = Mathf.Abs(delta.x) <= passZoneHalfWidth && Mathf.Abs(delta.y) <= passZoneHalfHeight;
        if (!insideZone)
        {
            if (trackingPass)
                ResetPassTracking();

            return;
        }

        int side = GetSide(delta.x);
        if (!trackingPass)
        {
            if (side != 0)
            {
                trackingPass = true;
                trackedEntrySide = side;
            }

            return;
        }

        if (side == 0 || side == trackedEntrySide)
            return;

        NotifyPlayerPassed();
    }

    private int GetSide(float x)
    {
        if (x > sideEpsilon)
            return 1;

        if (x < -sideEpsilon)
            return -1;

        return 0;
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null)
            return;

        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
            playerTransform = playerController.transform;
    }

    private void CachePassZoneOrigin()
    {
        if (targetDoor == null || hasPassZoneOrigin)
            return;

        passZoneOriginWorld = targetDoor.transform.position;
        hasPassZoneOrigin = true;
    }

    private void ResetPassTracking()
    {
        trackingPass = false;
        trackedEntrySide = 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetDoor == null)
            targetDoor = GetComponent<SwitchDoor>();

        passZoneHalfWidth = Mathf.Max(0.1f, passZoneHalfWidth);
        passZoneHalfHeight = Mathf.Max(0.1f, passZoneHalfHeight);
        sideEpsilon = Mathf.Max(0f, sideEpsilon);
    }
#endif
}
