using UnityEngine;

// Pressure plate button. Activates any linked IButtonActivatable target
// (SwitchDoor, MovingPlatform, etc.) when a Box or Player enters the trigger
// zone. Deactivates when all activators leave.
// Attach a BoxCollider2D (isTrigger = true) to define the activation zone.
[RequireComponent(typeof(BoxCollider2D))]
public class ButtonController : MonoBehaviour, ISnapshotSaveable
{
    [Tooltip("Drag any GameObject that has an IButtonActivatable component (SwitchDoor, MovingPlatform with buttonControlled, etc.)")]
    [SerializeField] private GameObject targetObject;

    // Keep the old field so existing scenes that had a SwitchDoor assigned
    // don't lose their reference. On first Awake the value is auto-migrated.
    [HideInInspector] [SerializeField] private SwitchDoor targetDoor;

    [SerializeField] private Sprite unpressedSprite;
    [SerializeField] private Sprite pressedSprite;

    private SpriteRenderer sr;
    private int overlapCount;
    private bool isPressed;
    private IButtonActivatable activatable;

    [System.Serializable]
    private class SnapshotState
    {
        public int overlapCount;
        public bool isPressed;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Auto-migrate from the legacy targetDoor field
        if (targetObject == null && targetDoor != null)
        {
            targetObject = targetDoor.gameObject;
        }

        // Resolve the interface from the target GameObject
        if (targetObject != null)
        {
            activatable = targetObject.GetComponent<IButtonActivatable>();
            if (activatable == null)
                Debug.LogWarning($"[ButtonController] targetObject '{targetObject.name}' does not implement IButtonActivatable.", this);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Box") && !other.CompareTag("Player")) return;
        overlapCount++;
        if (overlapCount == 1) SetState(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Box") && !other.CompareTag("Player")) return;
        overlapCount = Mathf.Max(0, overlapCount - 1);
        if (overlapCount == 0) SetState(false);
    }

    void SetState(bool pressed)
    {
        isPressed = pressed;

        if (sr != null)
            sr.enabled = !pressed;   // hide button when something is on it

        if (activatable == null) return;
        if (pressed) activatable.Activate();
        else         activatable.Deactivate();
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            overlapCount = overlapCount,
            isPressed = isPressed
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        overlapCount = Mathf.Max(0, snapshot.overlapCount);
        SetState(snapshot.isPressed);
    }
}
