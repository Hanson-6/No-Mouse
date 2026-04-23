using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[DefaultExecutionOrder(100)]
public class DarkVisionController : MonoBehaviour, ISnapshotSaveable
{
    private const int ShaderLampMax = 16;

    private static readonly int CenterWorldId = Shader.PropertyToID("_CenterWorld");
    private static readonly int RadiusWorldId = Shader.PropertyToID("_RadiusWorld");
    private static readonly int SoftnessWorldId = Shader.PropertyToID("_SoftnessWorld");
    private static readonly int BlackColorId = Shader.PropertyToID("_BlackColor");
    private static readonly int DarkFadeId = Shader.PropertyToID("_DarkFade");
    private static readonly int LampCountId = Shader.PropertyToID("_LampCount");
    private static readonly int LampCentersWorldId = Shader.PropertyToID("_LampCentersWorld");
    private static readonly int LampRadiiSoftnessId = Shader.PropertyToID("_LampRadiiSoftness");

    [Header("Runtime")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 focusOffsetWorld = Vector2.zero;

    [Header("Vision")]
    [SerializeField, Min(0.05f)] private float visibleRadius = 2f;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.05f;

    [Header("Visual")]
    [SerializeField] private Color blackoutColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Min(0f)] private float fadeDuration = 0f;
    [SerializeField] private int sortingOrder = 5000;
    [SerializeField, Min(1f)] private float viewportOverscan = 1.04f;

    [Header("Lamp Vision")]
    [SerializeField, Range(0, ShaderLampMax)] private int maxLampCount = ShaderLampMax;

    private Material runtimeMaterial;
    private GameObject overlayRoot;
    private Transform overlayTransform;
    private SpriteRenderer overlayRenderer;
    private Collider2D playerCollider;

    private float currentFade;
    private bool wantedDark;
    private readonly Vector4[] lampCentersBuffer = new Vector4[ShaderLampMax];
    private readonly Vector4[] lampRadiiSoftnessBuffer = new Vector4[ShaderLampMax];

    [System.Serializable]
    private class SnapshotState
    {
        public float currentFade;
    }

    private void Awake()
    {
        Shader shader = Shader.Find("Hidden/DarkVisionMask");
        if (shader == null)
        {
            Debug.LogError("[DarkVisionController] Missing shader Hidden/DarkVisionMask.");
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        EnsureOverlayObject();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveReferences();
        EnsureOverlayObject();
        ApplyMaterialProperties(currentFade);
    }

    private void Start()
    {
        ResolveReferences();
        EnsureOverlayObject();
        ApplyMaterialProperties(currentFade);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        if (overlayRoot != null)
            Destroy(overlayRoot);
    }

    private void Update()
    {
        wantedDark = GameData.IsDarkModeActive;

        float target = wantedDark ? 1f : 0f;
        if (fadeDuration <= 0f)
            currentFade = target;
        else
            currentFade = Mathf.MoveTowards(currentFade, target, Time.unscaledDeltaTime / fadeDuration);

        if (overlayRoot != null)
        {
            bool visible = wantedDark || currentFade > 0.001f;
            if (overlayRoot.activeSelf != visible)
                overlayRoot.SetActive(visible);
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsureOverlayObject();
        SyncOverlayToCamera();

        ApplyMaterialProperties(currentFade);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();
        EnsureOverlayObject();
        SyncOverlayToCamera();
        ApplyMaterialProperties(currentFade);
    }

    private void EnsureOverlayObject()
    {
        if (overlayRoot == null)
        {
            overlayRoot = new GameObject("DarkVisionOverlay");
            overlayRoot.hideFlags = HideFlags.HideInHierarchy;
            overlayTransform = overlayRoot.transform;

            overlayRenderer = overlayRoot.AddComponent<SpriteRenderer>();
            overlayRenderer.drawMode = SpriteDrawMode.Simple;
            overlayRenderer.color = Color.white;
            overlayRenderer.sortingLayerID = 0;
            overlayRenderer.sortingOrder = sortingOrder;
            overlayRenderer.sprite = GetFullscreenSprite();
            overlayRenderer.maskInteraction = SpriteMaskInteraction.None;
            overlayRenderer.material = runtimeMaterial;
        }

        if (overlayRenderer != null && overlayRenderer.material != runtimeMaterial)
            overlayRenderer.material = runtimeMaterial;

        if (overlayRenderer != null && overlayRenderer.sprite == null)
            overlayRenderer.sprite = GetFullscreenSprite();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            playerCollider = null;
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
                playerCollider = playerController.GetComponent<Collider2D>();
            }
        }
        else if (playerCollider == null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();
    }

    private void SyncOverlayToCamera()
    {
        if (targetCamera == null || overlayTransform == null)
            return;

        float z = targetCamera.transform.position.z + Mathf.Abs(targetCamera.nearClipPlane) + 0.01f;
        overlayTransform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, z);

        float worldHeight = targetCamera.orthographicSize * 2f * viewportOverscan;
        float worldWidth = worldHeight * targetCamera.aspect;
        overlayTransform.localScale = new Vector3(worldWidth, worldHeight, 1f);
    }

    private void ApplyMaterialProperties(float fade)
    {
        if (runtimeMaterial == null)
            return;

        Vector3 playerFocusWorld = GetPlayerFocusWorldPosition();

        runtimeMaterial.SetVector(CenterWorldId, new Vector4(playerFocusWorld.x, playerFocusWorld.y, 0f, 0f));
        runtimeMaterial.SetFloat(RadiusWorldId, Mathf.Max(0.01f, visibleRadius));
        runtimeMaterial.SetFloat(SoftnessWorldId, Mathf.Max(0.0001f, edgeSoftness));
        runtimeMaterial.SetColor(BlackColorId, blackoutColor);
        runtimeMaterial.SetFloat(DarkFadeId, Mathf.Clamp01(fade));

        ApplyLampProperties();
    }

    private void ApplyLampProperties()
    {
        int maxCount = Mathf.Clamp(maxLampCount, 0, ShaderLampMax);
        int lampCount = 0;

        IReadOnlyList<DarkVisionLamp> lamps = DarkVisionLamp.ActiveLamps;
        int sourceCount = lamps.Count;

        for (int i = 0; i < sourceCount && lampCount < maxCount; i++)
        {
            DarkVisionLamp lamp = lamps[i];
            if (lamp == null || !lamp.isActiveAndEnabled)
                continue;

            Vector2 center = lamp.GetWorldCenter();
            lampCentersBuffer[lampCount] = new Vector4(center.x, center.y, 0f, 0f);
            lampRadiiSoftnessBuffer[lampCount] = new Vector4(lamp.VisibleRadius, lamp.EdgeSoftness, 0f, 0f);
            lampCount++;
        }

        runtimeMaterial.SetFloat(LampCountId, lampCount);
        runtimeMaterial.SetVectorArray(LampCentersWorldId, lampCentersBuffer);
        runtimeMaterial.SetVectorArray(LampRadiiSoftnessId, lampRadiiSoftnessBuffer);
    }

    private static Sprite GetFullscreenSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    public void SetPlayer(PlayerController target)
    {
        player = target != null ? target.transform : null;
        playerCollider = target != null ? target.GetComponent<Collider2D>() : null;
    }

    private Vector3 GetPlayerFocusWorldPosition()
    {
        if (playerCollider != null)
            return playerCollider.bounds.center + (Vector3)focusOffsetWorld;

        if (player != null)
            return player.position + (Vector3)focusOffsetWorld;

        return Vector3.zero;
    }

    public string CaptureSnapshotState()
    {
        return JsonUtility.ToJson(new SnapshotState
        {
            currentFade = currentFade
        });
    }

    public void RestoreSnapshotState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        SnapshotState state = JsonUtility.FromJson<SnapshotState>(stateJson);
        currentFade = Mathf.Clamp01(state != null ? state.currentFade : 0f);
        wantedDark = GameData.IsDarkModeActive;
        ResolveReferences();
        EnsureOverlayObject();
        ApplyMaterialProperties(currentFade);
    }
}
