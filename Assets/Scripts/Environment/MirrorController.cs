using UnityEngine;
using GestureRecognition.Core;
using System.Collections.Generic;

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

    [Header("Mirror Barrier")]
    [SerializeField] private bool enableInvisibleBarrier = true;
    [SerializeField, Min(0f)] private float blockerThickness = 0f;
    [SerializeField, Min(0f)] private float blockerVerticalRange = 0f;
    [SerializeField] private string blockerObjectName = "MirrorBlocker";
    [SerializeField] private PhysicsMaterial2D blockerMaterial;
    [SerializeField] private BoxCollider2D blockerCollider;

    [Header("Mirror Clone")]
    [SerializeField] private Color cloneTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private string cloneObjectName = "MirrorClone";

    [Header("Switch Validation")]
    [SerializeField] private bool cancelSwapIfBlocked = true;
    [SerializeField] private LayerMask swapBlockerLayers;
    [SerializeField, Min(0f)] private float swapValidationInset = 0.02f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float switchCooldown = 2f;

    private BoxCollider2D mirrorCollider;
    private Transform playerTransform;
    private PlayerController playerController;
    private Rigidbody2D playerRb;
    private SpriteRenderer playerSprite;
    private Collider2D playerCollider;

    private GameObject cloneObject;
    private SpriteRenderer cloneSprite;
    private readonly Collider2D[] swapOverlapHits = new Collider2D[8];

    private bool pendingSwap;
    private bool playerInsideZone;
    private float lastSwitchTime = -Mathf.Infinity;

    private static readonly List<MirrorController> activeMirrors = new List<MirrorController>();
    private static int activeZoneCount;
    public static bool IsPlayerInAnyMirrorZone => activeZoneCount > 0;

    public static bool TryGetMirrorFocusForPosition(Vector3 worldPosition, out Vector3 focusPosition)
    {
        focusPosition = Vector3.zero;
        float bestScore = float.MaxValue;
        bool found = false;

        for (int i = 0; i < activeMirrors.Count; i++)
        {
            MirrorController mirror = activeMirrors[i];
            if (mirror == null || !mirror.isActiveAndEnabled)
                continue;

            if (!mirror.ContainsPosition(worldPosition))
                continue;

            float score = Mathf.Abs(worldPosition.x - mirror.transform.position.x);
            if (score < bestScore)
            {
                bestScore = score;
                focusPosition = mirror.transform.position;
                found = true;
            }
        }

        return found;
    }

    void Awake()
    {
        mirrorCollider = GetComponent<BoxCollider2D>();
        EnsureZoneReferences();
        EnsureBlockerReference();
        EnsureSwapBlockerLayers();
        RefreshZoneVisuals();
        RefreshBlockerCollider();
    }

    void OnEnable()
    {
        mirrorCollider = GetComponent<BoxCollider2D>();
        if (!activeMirrors.Contains(this))
            activeMirrors.Add(this);

        GestureEvents.OnGestureChanged += OnGestureChanged;
        EnsureZoneReferences();
        EnsureBlockerReference();
        EnsureSwapBlockerLayers();
        RefreshZoneVisuals();
        RefreshBlockerCollider();
    }

    void OnDisable()
    {
        activeMirrors.Remove(this);
        GestureEvents.OnGestureChanged -= OnGestureChanged;
        pendingSwap = false;
        SetPlayerInZone(false);
        DestroyClone();
    }

    void OnDestroy()
    {
        activeMirrors.Remove(this);
        SetPlayerInZone(false);
    }

    void OnValidate()
    {
        horizontalRange = Mathf.Max(0.5f, horizontalRange);
        verticalRange = Mathf.Max(0f, verticalRange);
        blockerThickness = Mathf.Max(0f, blockerThickness);
        blockerVerticalRange = Mathf.Max(0f, blockerVerticalRange);
        swapValidationInset = Mathf.Max(0f, swapValidationInset);
        EnsureZoneReferences();
        EnsureBlockerReference();
        EnsureSwapBlockerLayers();
        RefreshZoneVisuals();
        RefreshBlockerCollider();
    }

    void LateUpdate()
    {
        EnsureZoneReferences();
        EnsureBlockerReference();
        RefreshZoneVisuals();
        RefreshBlockerCollider();

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
        bool inMirrorZone = ContainsPosition(playerPos);

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
            TrySwapPlayerTo(mirroredPosition, dx);
            pendingSwap = false;
        }
    }

    private bool ContainsPosition(Vector3 worldPosition)
    {
        float dx = worldPosition.x - transform.position.x;
        float dy = Mathf.Abs(worldPosition.y - transform.position.y);
        return Mathf.Abs(dx) <= horizontalRange && dy <= GetActiveVerticalHalfRange();
    }

    private void OnGestureChanged(GestureResult result)
    {
        if (result.Type == GestureType.Switch)
        {
            if (Time.time - lastSwitchTime >= switchCooldown)
            {
                pendingSwap = true;
                lastSwitchTime = Time.time;
            }
        }
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

    private void EnsureBlockerReference()
    {
        if (string.IsNullOrWhiteSpace(blockerObjectName))
            blockerObjectName = "MirrorBlocker";

        if (blockerCollider == mirrorCollider)
            blockerCollider = null;

        if (blockerCollider == null)
        {
            Transform existing = transform.Find(blockerObjectName);
            if (existing != null)
                blockerCollider = existing.GetComponent<BoxCollider2D>();
        }

        if (blockerCollider == null && enableInvisibleBarrier)
        {
            GameObject blocker = new GameObject(blockerObjectName);
            blocker.transform.SetParent(transform, false);
            blockerCollider = blocker.AddComponent<BoxCollider2D>();
        }

        if (blockerCollider == null)
            return;

        blockerCollider.isTrigger = false;
        blockerCollider.usedByEffector = false;
        blockerCollider.sharedMaterial = blockerMaterial;
        blockerCollider.enabled = enableInvisibleBarrier;

        GameObject blockerObject = blockerCollider.gameObject;
        if (blockerObject.layer != gameObject.layer)
            blockerObject.layer = gameObject.layer;
    }

    private void EnsureSwapBlockerLayers()
    {
        if (swapBlockerLayers.value != 0)
            return;

        int groundMask = LayerMask.GetMask("Ground");
        swapBlockerLayers = groundMask != 0
            ? groundMask
            : Physics2D.DefaultRaycastLayers;
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

    private void RefreshBlockerCollider()
    {
        if (blockerCollider == null)
            return;

        blockerCollider.enabled = enableInvisibleBarrier;
        if (!enableInvisibleBarrier)
            return;

        Transform blockerTransform = blockerCollider.transform;
        blockerTransform.localPosition = Vector3.zero;
        blockerTransform.localRotation = Quaternion.identity;
        blockerTransform.localScale = Vector3.one;

        float parentScaleX = Mathf.Abs(transform.lossyScale.x);
        float parentScaleY = Mathf.Abs(transform.lossyScale.y);
        if (parentScaleX < 0.0001f) parentScaleX = 1f;
        if (parentScaleY < 0.0001f) parentScaleY = 1f;

        float worldWidth = GetBlockerThicknessWorld();
        float worldHeight = GetBlockerHeightWorld();

        blockerCollider.offset = Vector2.zero;
        blockerCollider.size = new Vector2(
            Mathf.Max(0.01f, worldWidth / parentScaleX),
            Mathf.Max(0.01f, worldHeight / parentScaleY));
    }

    private float GetActiveVerticalHalfRange()
    {
        if (verticalRange > 0f)
            return verticalRange;

        return GetMirrorHeightWorld() * 0.5f;
    }

    private float GetBlockerHeightWorld()
    {
        float halfRange = blockerVerticalRange > 0f
            ? blockerVerticalRange
            : GetActiveVerticalHalfRange();

        return Mathf.Max(GetMirrorHeightWorld(), halfRange * 2f);
    }

    private float GetBlockerThicknessWorld()
    {
        if (blockerThickness > 0f)
            return blockerThickness;

        if (mirrorCollider != null)
            return Mathf.Max(0.05f, mirrorCollider.bounds.size.x);

        return 0.1f;
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

        if (playerCollider == null)
            playerCollider = playerTransform.GetComponent<Collider2D>();
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

    private bool IsSwapDestinationBlocked(Vector3 destination)
    {
        if (!cancelSwapIfBlocked)
            return false;

        if (playerCollider == null)
            return false;

        if (swapBlockerLayers.value == 0)
            return false;

        Bounds bounds = playerCollider.bounds;
        float width = Mathf.Max(0.05f, bounds.size.x - swapValidationInset * 2f);
        float height = Mathf.Max(0.05f, bounds.size.y - swapValidationInset * 2f);

        Vector2 centerOffset = playerTransform != null
            ? (Vector2)(playerCollider.bounds.center - playerTransform.position)
            : playerCollider.offset;
        Vector2 checkCenter = new Vector2(destination.x + centerOffset.x, destination.y + centerOffset.y);
        Vector2 checkSize = new Vector2(width, height);

        ContactFilter2D overlapFilter = new ContactFilter2D();
        overlapFilter.SetLayerMask(swapBlockerLayers);
        overlapFilter.useTriggers = false;

        int hitCount = Physics2D.OverlapBox(checkCenter, checkSize, 0f, overlapFilter, swapOverlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = swapOverlapHits[i];
            if (hit == null)
                continue;

            if (ShouldIgnoreSwapBlocker(hit))
                continue;

            return true;
        }

        return false;
    }

    private bool ShouldIgnoreSwapBlocker(Collider2D candidate)
    {
        if (candidate == null)
            return true;

        if (candidate == mirrorCollider || candidate == blockerCollider)
            return true;

        if (playerCollider != null && candidate == playerCollider)
            return true;

        if (playerTransform != null && candidate.transform.IsChildOf(playerTransform))
            return true;

        if (candidate.transform.IsChildOf(transform))
            return true;

        if (cloneObject != null && candidate.transform.IsChildOf(cloneObject.transform))
            return true;

        return false;
    }

    private void TrySwapPlayerTo(Vector3 mirroredPosition, float dx)
    {
        if (playerTransform == null)
            return;

        Vector3 swapDestination = mirroredPosition;
        float minDistance = GetMinimumSwapDistance();
        float absDx = Mathf.Abs(dx);
        if (absDx < minDistance)
        {
            float sign = Mathf.Sign(dx);
            if (Mathf.Approximately(sign, 0f))
                sign = playerSprite != null && playerSprite.flipX ? -1f : 1f;

            swapDestination.x = transform.position.x - sign * minDistance;
        }

        if (IsSwapDestinationBlocked(swapDestination))
            return;

        if (playerRb != null)
        {
            Vector2 oldVelocity = playerRb.velocity;
            playerRb.position = new Vector2(swapDestination.x, swapDestination.y);
            playerRb.velocity = new Vector2(-oldVelocity.x, oldVelocity.y);
        }
        else
        {
            playerTransform.position = swapDestination;
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

        if (blockerCollider != null && blockerCollider.enabled)
            mirrorHalfWidth = Mathf.Max(mirrorHalfWidth, blockerCollider.bounds.extents.x);

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
