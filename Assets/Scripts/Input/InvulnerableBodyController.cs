using GestureRecognition.Core;
using UnityEngine;

public class InvulnerableBodyController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField, Min(0.01f)] private float stationaryVelocityThreshold = 0.08f;
    [SerializeField] private bool emitLogs;

    private bool invulnerableGestureDetected;
    private bool invulnerableBodyActive;

    void Awake()
    {
        if (player == null)
            player = FindObjectOfType<PlayerController>();
    }

    public void SetPlayer(PlayerController target)
    {
        if (invulnerableBodyActive && player != null)
            player.SetInvulnerableBodyActive(false);

        player = target;
        invulnerableBodyActive = false;
        invulnerableGestureDetected = false;
    }

    void OnEnable()
    {
        GestureEvents.OnGestureUpdated += OnGestureUpdated;
    }

    void OnDisable()
    {
        GestureEvents.OnGestureUpdated -= OnGestureUpdated;
        SetInvulnerableBodyActive(false, "controller-disabled");
        invulnerableGestureDetected = false;
    }

    void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
            if (player == null)
                return;
        }

        if (!invulnerableGestureDetected)
        {
            SetInvulnerableBodyActive(false, "gesture-lost");
            return;
        }

        if (!invulnerableBodyActive && player.CanActivateInvulnerableBody(stationaryVelocityThreshold))
            SetInvulnerableBodyActive(true, "gesture-activated");
    }

    private void OnGestureUpdated(GestureResult result)
    {
        invulnerableGestureDetected = result.Type == GestureType.InvulnerableBody;

        if (!invulnerableGestureDetected)
            SetInvulnerableBodyActive(false, "gesture-not-detected");
    }

    private void SetInvulnerableBodyActive(bool active, string reason)
    {
        if (invulnerableBodyActive == active)
            return;

        invulnerableBodyActive = active;

        if (player != null)
            player.SetInvulnerableBodyActive(active);

        if (emitLogs)
            Debug.Log($"[InvulnerableBody] active={active} reason={reason}");
    }
}
