using UnityEngine;
using System.Collections;

// A door that slides upward when opened and drops back down when closed.
// Driven by ButtonController. The collider is disabled while the door is fully open.
[RequireComponent(typeof(Collider2D))]
public class SwitchDoor : MonoBehaviour, ISnapshotSaveable, IButtonActivatable
{
    [Tooltip("World-space units to slide upward when opening.")]
    [SerializeField] private float openOffset = 1f;
    [SerializeField] private float slideSpeed = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private float openSoundVolume = 1f;

    private Vector3 closedPos;
    private Vector3 openPos;
    private Collider2D col;
    private AudioSource audioSource;
    private bool isOpen;

    [System.Serializable]
    private class SnapshotState
    {
        public float positionX;
        public float positionY;
        public float positionZ;
        public bool colliderEnabled;
        public bool isOpen;
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        closedPos = transform.position;
        openPos   = closedPos + Vector3.up * openOffset;
    }

    // --- IButtonActivatable ---
    public void Activate()   => Open();
    public void Deactivate() => Close();

    public void Open()
    {
        StopAllCoroutines();
        isOpen = true;
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound, openSoundVolume);
        StartCoroutine(SlideTo(openPos, disableColliderWhenDone: false));
    }

    public void Close()
    {
        StopAllCoroutines();
        isOpen = false;
        col.enabled = true; // re-enable before sliding back so player can't pass through
        if (audioSource != null && closeSound != null) audioSource.PlayOneShot(closeSound);
        StartCoroutine(SlideTo(closedPos, disableColliderWhenDone: false));
    }

    IEnumerator SlideTo(Vector3 target, bool disableColliderWhenDone)
    {
        while ((transform.position - target).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, slideSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
        if (disableColliderWhenDone) col.enabled = false;
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            positionX = transform.position.x,
            positionY = transform.position.y,
            positionZ = transform.position.z,
            colliderEnabled = col != null && col.enabled,
            isOpen = isOpen
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);
        StopAllCoroutines();
        transform.position = new Vector3(snapshot.positionX, snapshot.positionY, snapshot.positionZ);

        if (col != null)
            col.enabled = snapshot.colliderEnabled;

        isOpen = snapshot.isOpen;
    }
}
