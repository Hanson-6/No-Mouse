using UnityEngine;

[DisallowMultipleComponent]
public sealed class DarkModeDoorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SwitchDoor targetDoor;

    [Header("Behavior")]
    [SerializeField] private bool openWhenDark = true;

    private bool hasAppliedState;
    private bool lastDarkMode;

    private void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponent<SwitchDoor>();
    }

    private void OnEnable()
    {
        ApplyDarkModeState(force: true);
    }

    private void Update()
    {
        ApplyDarkModeState(force: false);
    }

    private void ApplyDarkModeState(bool force)
    {
        if (targetDoor == null)
            return;

        bool isDarkMode = GameData.IsDarkModeActive;
        if (!force && hasAppliedState && isDarkMode == lastDarkMode)
            return;

        bool shouldOpen = openWhenDark ? isDarkMode : !isDarkMode;
        if (shouldOpen)
            targetDoor.Open();
        else
            targetDoor.Close();

        lastDarkMode = isDarkMode;
        hasAppliedState = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetDoor == null)
            targetDoor = GetComponent<SwitchDoor>();
    }
#endif
}
