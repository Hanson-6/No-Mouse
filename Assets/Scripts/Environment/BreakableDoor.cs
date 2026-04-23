using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BreakableDoor : MonoBehaviour, ISnapshotSaveable
{
    [SerializeField] private bool disableGameObjectOnBreak = true;

    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private bool isBroken;

    [System.Serializable]
    private class SnapshotState
    {
        public bool activeSelf;
        public bool isBroken;
        public bool colliderEnabled;
        public bool rendererEnabled;
    }

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Break()
    {
        if (isBroken)
            return;

        isBroken = true;

        if (disableGameObjectOnBreak)
        {
            gameObject.SetActive(false);
            return;
        }

        if (boxCollider != null)
            boxCollider.enabled = false;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    public string CaptureSnapshotState()
    {
        var snapshot = new SnapshotState
        {
            activeSelf = gameObject.activeSelf,
            isBroken = isBroken,
            colliderEnabled = boxCollider != null && boxCollider.enabled,
            rendererEnabled = spriteRenderer != null && spriteRenderer.enabled
        };

        return JsonUtility.ToJson(snapshot);
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState snapshot = JsonUtility.FromJson<SnapshotState>(stateJson);

        isBroken = snapshot.isBroken;

        if (boxCollider != null)
            boxCollider.enabled = snapshot.colliderEnabled;

        if (spriteRenderer != null)
            spriteRenderer.enabled = snapshot.rendererEnabled;

        gameObject.SetActive(snapshot.activeSelf);
    }
}
