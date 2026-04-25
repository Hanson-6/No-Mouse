using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class StoneTrap : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float waitAtTop = 2f;
    [SerializeField, Min(0f)] private float waitOnGround = 2f;

    [Header("Speed")]
    [SerializeField, Min(0.1f)] private float riseSpeed = 22f;
    [SerializeField, Min(0.1f)] private float fallSpeed = 22f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField, Min(0f)] private float castInset = 0.02f;
    [SerializeField, Min(0f)] private float snapToGroundOffset = 0.005f;
    [SerializeField, Min(0.5f)] private float fallbackDropDistance = 8f;

    [Header("Audio")]
    [SerializeField] private AudioClip fallStartSound;
    [SerializeField, Min(0f)] private float fallStartSoundVolume = 1f;
    [SerializeField] private AudioClip hitGroundSound;
    [SerializeField, Min(0f)] private float hitGroundSoundVolume = 1f;

    private enum State
    {
        WaitingTop,
        Falling,
        WaitingOnGround,
        Rising
    }

    private const float ArriveThreshold = 0.0001f;

    private BoxCollider2D triggerCollider;
    private AudioSource audioSource;
    private State state;
    private float stateTimer;
    private Vector3 topPosition;
    private float fallbackGroundY;

    void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        audioSource = GetComponent<AudioSource>();
        triggerCollider.isTrigger = true;
    }

    void OnValidate()
    {
        waitAtTop = Mathf.Max(0f, waitAtTop);
        waitOnGround = Mathf.Max(0f, waitOnGround);
        riseSpeed = Mathf.Max(0.1f, riseSpeed);
        fallSpeed = Mathf.Max(0.1f, fallSpeed);
        castInset = Mathf.Max(0f, castInset);
        snapToGroundOffset = Mathf.Max(0f, snapToGroundOffset);
        fallbackDropDistance = Mathf.Max(0.5f, fallbackDropDistance);
        fallStartSoundVolume = Mathf.Max(0f, fallStartSoundVolume);
        hitGroundSoundVolume = Mathf.Max(0f, hitGroundSoundVolume);

        if (groundLayerMask == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayerMask = 1 << groundLayer;
        }

        if (!Application.isPlaying)
        {
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
                col.isTrigger = true;
        }
    }

    void Start()
    {
        ResolveGroundLayerMask();

        topPosition = transform.position;
        fallbackGroundY = topPosition.y - fallbackDropDistance;

        EnterState(State.WaitingTop);
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingTop:
                stateTimer += Time.deltaTime;
                if (stateTimer >= waitAtTop)
                    EnterState(State.Falling);
                break;

            case State.Falling:
                UpdateFalling();
                break;

            case State.WaitingOnGround:
                stateTimer += Time.deltaTime;
                if (stateTimer >= waitOnGround)
                    EnterState(State.Rising);
                break;

            case State.Rising:
                transform.position = Vector3.MoveTowards(transform.position, topPosition, riseSpeed * Time.deltaTime);
                if ((transform.position - topPosition).sqrMagnitude <= ArriveThreshold)
                {
                    transform.position = topPosition;
                    EnterState(State.WaitingTop);
                }
                break;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryKillPlayer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryKillPlayer(other);
    }

    private static void TryKillPlayer(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        other.GetComponent<PlayerController>()?.Die();
    }

    private void ResolveGroundLayerMask()
    {
        if (groundLayerMask != 0)
            return;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            groundLayerMask = 1 << groundLayer;
            return;
        }

        groundLayerMask = Physics2D.DefaultRaycastLayers;
    }

    private void UpdateFalling()
    {
        float moveDistance = fallSpeed * Time.deltaTime;

        if (TryGetGroundHit(moveDistance, out RaycastHit2D hit))
        {
            float groundY = hit.point.y + triggerCollider.bounds.extents.y + snapToGroundOffset;
            transform.position = new Vector3(topPosition.x, groundY, transform.position.z);
            EnterState(State.WaitingOnGround);
            return;
        }

        float nextY = transform.position.y - moveDistance;
        if (nextY <= fallbackGroundY)
        {
            transform.position = new Vector3(topPosition.x, fallbackGroundY, transform.position.z);
            EnterState(State.WaitingOnGround);
            return;
        }

        transform.position = new Vector3(topPosition.x, nextY, transform.position.z);
    }

    private bool TryGetGroundHit(float moveDistance, out RaycastHit2D hit)
    {
        Bounds bounds = triggerCollider.bounds;
        Vector2 castOrigin = bounds.center;
        float castWidth = Mathf.Max(0.01f, bounds.size.x - castInset * 2f);
        float castHeight = Mathf.Max(0.01f, bounds.size.y - castInset * 2f);
        float castDistance = moveDistance + snapToGroundOffset + 0.001f;

        hit = Physics2D.BoxCast(castOrigin, new Vector2(castWidth, castHeight), 0f, Vector2.down, castDistance, groundLayerMask);
        return hit.collider != null;
    }

    private void EnterState(State nextState)
    {
        State previousState = state;
        state = nextState;
        stateTimer = 0f;
        PlayStateTransitionSound(previousState, nextState);
    }

    private void PlayStateTransitionSound(State fromState, State toState)
    {
        if (audioSource == null)
            return;

        if (toState == State.Falling && fromState != State.Falling)
        {
            if (fallStartSound != null)
                audioSource.PlayOneShot(fallStartSound, fallStartSoundVolume);
            return;
        }

        if (toState == State.WaitingOnGround && fromState == State.Falling)
        {
            if (hitGroundSound != null)
                audioSource.PlayOneShot(hitGroundSound, hitGroundSoundVolume);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 top = Application.isPlaying ? topPosition : transform.position;
        Vector3 bottom = new Vector3(top.x, top.y - Mathf.Max(0.5f, fallbackDropDistance), top.z);

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawSphere(top, 0.12f);
        Gizmos.DrawSphere(bottom, 0.12f);
        Gizmos.DrawLine(top, bottom);
    }
}
