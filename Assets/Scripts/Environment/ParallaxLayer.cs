using UnityEngine;

[DefaultExecutionOrder(10)]    // 确保在 CameraFollow 之后执行
/// <summary>
/// Seamlessly tiles a SpriteRenderer and parallax-scrolls it with the camera.
/// Spawns a second copy of the sprite at runtime so the layer loops
/// horizontally without gaps.
///
/// Reuse: attach to any SpriteRenderer GameObject.  The sprite's world-space
/// width must be >= the camera's view width for a gap-free loop.
///
/// parallaxFactor:
///   0 = layer is world-fixed (no drift — appears to scroll fully against camera).
///   1 = layer is locked to camera (appears static on screen).
///   0.2 = good value for a distant mountain range.
///
/// lockYToCamera:
///   When true the layer's bottom edge is always pinned to the camera's bottom
///   edge, so the mountain stays visible even when the camera moves vertically.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("How fast this layer follows the camera.\n" +
             "0 = world-fixed (fullest parallax effect).\n" +
             "1 = camera-locked (no parallax, appears static).")]
    [Range(0f, 1f)]
    public float parallaxFactor = 0.2f;

    [Tooltip("Constant drift speed in world units per second (positive = rightward). " +
             "Useful for drifting clouds on top of the parallax movement.")]
    public float autoScrollSpeed = 0f;

    [Tooltip("Pin the layer's bottom edge to the camera's bottom edge so the mountain " +
             "is always visible regardless of how far the camera moves vertically.")]
    public bool lockYToCamera = true;

    [Tooltip("Additional vertical offset applied when lockYToCamera is true " +
             "(positive = move the layer up relative to the camera bottom).")]
    public float yOffset = 0f;

    // ── Private state ──────────────────────────────────────────────────────

    Camera    _camera;
    Transform _cam;
    Transform _copyTransform;   // second tile, always kept one sprite-width to the right
    float     _spriteWidth;
    float     _spriteHalfHeight;
    float     _lastCamX;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.LogError("[ParallaxLayer] No Main Camera found.", this);
            enabled = false;
            return;
        }
        _cam = _camera.transform;

        var sr = GetComponent<SpriteRenderer>();
        // bounds.size is in world units (pixels / PPU * localScale)
        _spriteWidth      = sr.bounds.size.x;
        _spriteHalfHeight = sr.bounds.size.y * 0.5f;

        if (_spriteWidth < 0.01f)
        {
            Debug.LogError("[ParallaxLayer] Sprite width is near-zero — assign a valid sprite.", this);
            enabled = false;
            return;
        }

        // Spawn a seamless tile copy to the right, parented alongside this object
        var copyGO = new GameObject(name + "_tile");
        copyGO.transform.SetParent(transform.parent, worldPositionStays: true);
        copyGO.layer = gameObject.layer;

        var copySR              = copyGO.AddComponent<SpriteRenderer>();
        copySR.sprite           = sr.sprite;
        copySR.sortingLayerName = sr.sortingLayerName;
        copySR.sortingOrder     = sr.sortingOrder;
        copySR.color            = sr.color;
        copySR.flipX            = sr.flipX;

        _copyTransform            = copyGO.transform;
        _copyTransform.localScale = transform.localScale;
        _copyTransform.position   = new Vector3(
            transform.position.x + _spriteWidth,
            transform.position.y,
            transform.position.z);

        _lastCamX = _cam.position.x;
    }

    void LateUpdate()
    {
        float camDelta = _cam.position.x - _lastCamX;
        _lastCamX = _cam.position.x;

        // ── X: parallax scroll + optional auto-drift ───────────────────────
        float moveX = camDelta * parallaxFactor + autoScrollSpeed * Time.deltaTime;
        transform.position += new Vector3(moveX, 0f, 0f);

        // Wrap: when the main tile exits the camera view, teleport it to the
        // opposite side so looping is seamless.
        float halfViewW = _camera.orthographicSize * _camera.aspect;

        if (transform.position.x + _spriteWidth < _cam.position.x - halfViewW)
            transform.position += new Vector3(_spriteWidth, 0f, 0f);
        else if (transform.position.x > _cam.position.x + halfViewW)
            transform.position -= new Vector3(_spriteWidth, 0f, 0f);

        // ── Y: optionally pin bottom edge to camera bottom ─────────────────
        float y = lockYToCamera
            ? _cam.position.y - _camera.orthographicSize + _spriteHalfHeight + yOffset
            : transform.position.y;

        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        // Keep the copy tile always one sprite-width ahead of the main tile
        _copyTransform.position = new Vector3(
            transform.position.x + _spriteWidth,
            y,
            transform.position.z);
    }

    void OnDestroy()
    {
        if (_copyTransform != null)
            Destroy(_copyTransform.gameObject);
    }
}
