using UnityEngine;
using GestureRecognition.Core;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class MirrorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer mirrorRenderer;
    [SerializeField] private SpriteRenderer leftZoneRenderer;
    [SerializeField] private SpriteRenderer rightZoneRenderer;
    [SerializeField] private Sprite shadowSprite;

    [Header("Mirror Zone")]
    [SerializeField, Min(0.5f)] private float horizontalRange = 5f;
    [SerializeField] private float verticalRange = 0f;
    [SerializeField] private Color zoneTint = new Color(0.58f, 0.58f, 0.58f, 0.55f);

    [Header("Mirror Clone")]
    [SerializeField] private Color cloneTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private string cloneObjectName = "MirrorClone";

    private BoxCollider2D mirrorCollider;
    private Transform playerTransform;
    private PlayerController playerController;
    private Rigidbody2D playerRb;
    private SpriteRenderer playerSprite;

    private GameObject cloneObject;
    private SpriteRenderer cloneSprite;

    private bool pendingSwap;
    private bool playerInsideZone;

    private static int activeZoneCount;
    public static bool IsPlayerInAnyMirrorZone => activeZoneCount > 0;

    void Awake()
    {
        mirrorCollider = GetComponent<BoxCollider2D>();
        EnsureZoneReferences();
        RefreshZoneVisuals();
    }

    void OnEnable()
    {
        mirrorCollider = GetComponent<BoxCollider2D>();
        GestureEvents.OnGestureChanged += OnGestureChanged;
        EnsureZoneReferences();
        RefreshZoneVisuals();
    }

    void OnDisable()
    {
        GestureEvents.OnGestureChanged -= OnGestureChanged;
        pendingSwap = false;
        SetPlayerInZone(false);
        DestroyClone();
    }

    void OnDestroy()
    {
        SetPlayerInZone(false);
    }

    void OnValidate()
    {
        horizontalRange = Mathf.Max(0.5f, horizontalRange);
        verticalRange = Mathf.Max(0f, verticalRange);
        EnsureZoneReferences();
        RefreshZoneVisuals();
    }

    void LateUpdate()
    {
        EnsureZoneReferences();
        RefreshZoneVisuals();

        TryBindPlayer();
        if (!HasValidPlayer())
        {
            pendingSwap = false;
            SetCloneVisible(false);
            SetPlayerInZone(false);
            return;
        }

        if (playerController.IsDead)
        {
            pendingSwap = false;
            SetCloneVisible(false);
            SetPlayerInZone(false);
            return;
        }

        Vector3 playerPos = playerTransform.position;
        float mirrorX = transform.position.x;
        float dx = playerPos.x - mirrorX;
        float dy = Mathf.Abs(playerPos.y - transform.position.y);

        float verticalHalfRange = GetActiveVerticalHalfRange();
        bool inHorizontalRange = Mathf.Abs(dx) <= horizontalRange;
        bool inVerticalRange = dy <= verticalHalfRange;
        bool inMirrorZone = inHorizontalRange && inVerticalRange;

        if (!inMirrorZone)
        {
            pendingSwap = false;
            SetCloneVisible(false);
            SetPlayerInZone(false);
            return;
        }

        SetPlayerInZone(true);

        EnsureClone();
        if (cloneObject == null || cloneSprite == null)
        {
            pendingSwap = false;
            return;
        }

        Vector3 mirroredPosition = new Vector3(mirrorX - dx, playerPos.y, playerPos.z);
        cloneObject.transform.position = mirroredPosition;
        cloneObject.transform.localScale = playerTransform.lossyScale;

        cloneSprite.sprite = playerSprite.sprite;
        cloneSprite.flipX = !playerSprite.flipX;
        cloneSprite.color = cloneTint;
        cloneSprite.sortingLayerID = playerSprite.sortingLayerID;
        cloneSprite.sortingOrder = playerSprite.sortingOrder;

        SetCloneVisible(true);

        if (pendingSwap)
        {
            SwapPlayerTo(mirroredPosition, dx);
            pendingSwap = false;
        }
    }

    private void OnGestureChanged(GestureResult result)
    {
        if (result.Type == GestureType.Switch)
            pendingSwap = true;
    }

    private void EnsureZoneReferences()
    {
        if (mirrorRenderer == null)
            mirrorRenderer = GetComponent<SpriteRenderer>();

        if (leftZoneRenderer == null)
            leftZoneRenderer = FindOrCreateZoneRenderer("ZoneLeft");

        if (rightZoneRenderer == null)
            rightZoneRenderer = FindOrCreateZoneRenderer("ZoneRight");

        if (shadowSprite != null)
        {
            if (leftZoneRenderer != null)
                leftZoneRenderer.sprite = shadowSprite;
            if (rightZoneRenderer != null)
                rightZoneRenderer.sprite = shadowSprite;
        }
    }

    private SpriteRenderer FindOrCreateZoneRenderer(string zoneName)
    {
        Transform existing = transform.Find(zoneName);
        if (existing == null)
        {
            GameObject zone = new GameObject(zoneName);
            zone.transform.SetParent(transform, false);
            existing = zone.transform;
        }

        SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = existing.gameObject.AddComponent<SpriteRenderer>();

        return renderer;
    }

    private void RefreshZoneVisuals()
    {
        if (leftZoneRenderer == null || rightZoneRenderer == null)
            return;

        if (shadowSprite != null)
        {
            leftZoneRenderer.sprite = shadowSprite;
            rightZoneRenderer.sprite = shadowSprite;
        }

        ApplyZoneStyle(leftZoneRenderer);
        ApplyZoneStyle(rightZoneRenderer);

        float zoneHeight = GetActiveVerticalHalfRange() * 2f;
        float worldWidth = horizontalRange;
        float worldHalfOffset = horizontalRange * 0.5f;

        float parentScaleX = Mathf.Abs(transform.lossyScale.x);
        float parentScaleY = Mathf.Abs(transform.lossyScale.y);
        if (parentScaleX < 0.0001f) parentScaleX = 1f;
        if (parentScaleY < 0.0001f) parentScaleY = 1f;

        leftZoneRenderer.transform.localPosition = new Vector3(-worldHalfOffset / parentScaleX, 0f, 0f);
        rightZoneRenderer.transform.localPosition = new Vector3(worldHalfOffset / parentScaleX, 0f, 0f);

        ApplyZoneScale(leftZoneRenderer, worldWidth, zoneHeight, parentScaleX, parentScaleY);
        ApplyZoneScale(rightZoneRenderer, worldWidth, zoneHeight, parentScaleX, parentScaleY);
    }

    private float GetActiveVerticalHalfRange()
    {
        if (verticalRange > 0f)
            return verticalRange;

        return GetMirrorHeightWorld() * 0.5f;
    }

    private void ApplyZoneStyle(SpriteRenderer zoneRenderer)
    {
        zoneRenderer.color = zoneTint;

        if (mirrorRenderer != null)
        {
            zoneRenderer.sortingLayerID = mirrorRenderer.sortingLayerID;
            zoneRenderer.sortingOrder = mirrorRenderer.sortingOrder - 1;
        }
    }

    private static void ApplyZoneScale(
        SpriteRenderer zoneRenderer,
        float worldWidth,
        float worldHeight,
        float parentScaleX,
        float parentScaleY)
    {
        if (zoneRenderer.sprite == null)
            return;

        float spriteWidth = zoneRenderer.sprite.bounds.size.x;
        float spriteHeight = zoneRenderer.sprite.bounds.size.y;
        if (spriteWidth <= 0f || spriteHeight <= 0f)
            return;

        float localScaleX = worldWidth / (spriteWidth * parentScaleX);
        float localScaleY = worldHeight / (spriteHeight * parentScaleY);
        zoneRenderer.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
    }

    private float GetMirrorHeightWorld()
    {
        if (mirrorCollider != null)
            return mirrorCollider.bounds.size.y;

        if (mirrorRenderer != null && mirrorRenderer.sprite != null)
            return mirrorRenderer.bounds.size.y;

        return 2f;
    }

    private void TryBindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }

        if (playerTransform == null)
            return;

        if (playerController == null)
            playerController = playerTransform.GetComponent<PlayerController>();

        if (playerRb == null)
            playerRb = playerTransform.GetComponent<Rigidbody2D>();

        if (playerSprite == null)
            playerSprite = playerTransform.GetComponent<SpriteRenderer>();
    }

    private bool HasValidPlayer()
    {
        return playerTransform != null
            && playerController != null
            && playerSprite != null;
    }

    private void EnsureClone()
    {
        if (cloneObject != null && cloneSprite != null)
            return;

        if (cloneObject == null)
            cloneObject = new GameObject(cloneObjectName);

        cloneSprite = cloneObject.GetComponent<SpriteRenderer>();
        if (cloneSprite == null)
            cloneSprite = cloneObject.AddComponent<SpriteRenderer>();

        if (cloneObject.GetComponent<SnapshotIgnore>() == null)
            cloneObject.AddComponent<SnapshotIgnore>();

        cloneObject.SetActive(false);
    }

    private void SwapPlayerTo(Vector3 mirroredPosition, float dx)
    {
        if (playerTransform == null)
            return;

        float minDistance = GetMinimumSwapDistance();
        float absDx = Mathf.Abs(dx);
        if (absDx < minDistance)
        {
            float sign = Mathf.Sign(dx);
            if (Mathf.Approximately(sign, 0f))
                sign = playerSprite != null && playerSprite.flipX ? -1f : 1f;

            mirroredPosition.x = transform.position.x - sign * minDistance;
        }

        if (playerRb != null)
        {
            Vector2 oldVelocity = playerRb.velocity;
            playerRb.position = new Vector2(mirroredPosition.x, mirroredPosition.y);
            playerRb.velocity = new Vector2(-oldVelocity.x, oldVelocity.y);
        }
        else
        {
            playerTransform.position = mirroredPosition;
        }
    }

    private float GetMinimumSwapDistance()
    {
        float playerHalfWidth = 0.1f;
        if (playerTransform != null)
        {
            Collider2D playerCollider = playerTransform.GetComponent<Collider2D>();
            if (playerCollider != null)
                playerHalfWidth = playerCollider.bounds.extents.x;
        }

        float mirrorHalfWidth = 0.05f;
        if (mirrorCollider != null)
            mirrorHalfWidth = mirrorCollider.bounds.extents.x;

        return playerHalfWidth + mirrorHalfWidth + 0.01f;
    }

    private void SetCloneVisible(bool visible)
    {
        if (cloneObject != null)
            cloneObject.SetActive(visible);
    }

    private void DestroyClone()
    {
        if (cloneObject == null)
            return;

        if (Application.isPlaying)
            Destroy(cloneObject);
        else
            DestroyImmediate(cloneObject);

        cloneObject = null;
        cloneSprite = null;
    }

    private void SetPlayerInZone(bool insideZone)
    {
        if (playerInsideZone == insideZone)
            return;

        playerInsideZone = insideZone;
        if (insideZone)
        {
            activeZoneCount++;
        }
        else
        {
            activeZoneCount = Mathf.Max(0, activeZoneCount - 1);
        }
    }
}
