using UnityEngine;
using System.Collections;

// A door that slides upward when opened and drops back down when closed.
// Driven by ButtonController. The collider is disabled while the door is fully open.
[RequireComponent(typeof(Collider2D))]
public class SwitchDoor : MonoBehaviour
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

    void Awake()
    {
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        closedPos = transform.position;
        openPos   = closedPos + Vector3.up * openOffset;
    }

    public void Open()
    {
        StopAllCoroutines();
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound, openSoundVolume);
        StartCoroutine(SlideTo(openPos, disableColliderWhenDone: true));
    }

    public void Close()
    {
        StopAllCoroutines();
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
}
