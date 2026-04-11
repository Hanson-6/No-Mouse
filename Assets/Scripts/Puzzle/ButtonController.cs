using UnityEngine;

// Pressure plate button. Activates the linked SwitchDoor when a Box or Player
// enters the trigger zone. Deactivates when all activators leave.
// Attach a BoxCollider2D (isTrigger = true) to define the activation zone.
[RequireComponent(typeof(BoxCollider2D))]
public class ButtonController : MonoBehaviour, ISnapshotSaveable
{
    [SerializeField] private SwitchDoor targetDoor;
    [SerializeField] private Sprite unpressedSprite;
    [SerializeField] private Sprite pressedSprite;

    private SpriteRenderer sr;
    private int overlapCount;
    private bool isPressed;

    [System.Serializable]
    private class SnapshotState
    {
        public int overlapCount;
        public bool isPressed;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
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

        if (targetDoor == null) return;
        if (pressed) targetDoor.Open();
        else         targetDoor.Close();
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
