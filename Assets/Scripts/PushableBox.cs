using UnityEngine;

// Marker component for the pushable box.
// All movement is handled by Rigidbody2D physics.
// Tag this GameObject "Box" so ButtonController can detect it.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PushableBox : MonoBehaviour { }
