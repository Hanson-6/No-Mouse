using UnityEngine;

// Pressure plate button. Activates the linked SwitchDoor when a Box or Player
// enters the trigger zone. Deactivates when all activators leave.
// Attach a BoxCollider2D (isTrigger = true) to define the activation zone.
[RequireComponent(typeof(BoxCollider2D))]
public class ButtonController : MonoBehaviour
{
    [SerializeField] private SwitchDoor targetDoor;
    [SerializeField] private Sprite unpressedSprite;
    [SerializeField] private Sprite pressedSprite;

    private SpriteRenderer sr;
    private int overlapCount;

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
        if (sr != null)
            sr.enabled = !pressed;   // hide button when something is on it

        if (targetDoor == null) return;
        if (pressed) targetDoor.Open();
        else         targetDoor.Close();
    }
}
