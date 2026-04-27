using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using LDtkUnity;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public class MirrorEntryTrigger : MonoBehaviour
{
    private const string DefaultVideoFileName = "switch_to_mirror.mp4";
    private const string DefaultLdtkEnemyIid = "7f0f75f0-21a0-11f1-8579-f735b60fe489";
#if UNITY_EDITOR
    private const string VideoAssetPath = "Assets/Textures/Videos/switch_to_mirror.mp4";
    private const string LeftWallPrefabPath = "Assets/Prefabs/LeftWall.prefab";
#endif

    [Header("Video")]
    [SerializeField] private VideoClip entryTransitionClip;
    [SerializeField] private string entryTransitionVideoFileName = DefaultVideoFileName;

    [Header("Teleport")]
    [SerializeField] private MirrorController targetMirror;
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private bool mirrorToMirroredSide = true;
    [SerializeField, Min(0f)] private float safeDistancePadding = 0.16f;

    [Header("LDtk Entity Target")]
    [SerializeField] private string ldtkEnemyIid = DefaultLdtkEnemyIid;
    [SerializeField] private bool findLdtkEntityAtRuntime = true;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Left Wall Lock")]
    [SerializeField] private GameObject leftWallPrefab;
    [SerializeField, Min(0f)] private float leftWallRightPadding = 0.05f;

    private bool hasTriggered;
    private bool isPlaying;
    private bool missingSourceWarned;
    private Coroutine playbackCoroutine;
    private BoxCollider2D triggerCollider;

    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private SpriteRenderer playerSprite;

    private GameObject overlayCanvas;
    private RenderTexture overlayTexture;

    private RigidbodyConstraints2D cachedConstraints;
    private bool hasCachedConstraints;

    private Vector3 cachedLdtkTargetPos;
    private bool hasCachedLdtkTargetPos;
    private GameObject spawnedLeftWall;

    void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        CacheLdtkTargetPosition();
    }

    void OnEnable()
    {
        TryAutoAssignClipInEditor();
        TryAutoAssignLeftWallPrefabInEditor();
    }

    void OnDisable()
    {
        StopPlayback();
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(entryTransitionVideoFileName))
            entryTransitionVideoFileName = DefaultVideoFileName;
        if (string.IsNullOrWhiteSpace(ldtkEnemyIid))
            ldtkEnemyIid = DefaultLdtkEnemyIid;
        safeDistancePadding = Mathf.Max(0f, safeDistancePadding);
        leftWallRightPadding = Mathf.Max(0f, leftWallRightPadding);
        TryAutoAssignClipInEditor();
        TryAutoAssignLeftWallPrefabInEditor();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (triggerOnlyOnce && hasTriggered)
            return;

        if (isPlaying)
            return;

        BindPlayer(other.transform);
        if (playerTransform == null)
            return;

        if (playbackCoroutine != null)
            StopCoroutine(playbackCoroutine);

        playbackCoroutine = StartCoroutine(PlayTransitionRoutine());
    }

    private void StopPlayback()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        isPlaying = false;
        ReleasePlayerLock();
        DestroyOverlay();
    }

    private void BindPlayer(Transform playerT)
    {
        if (playerTransform == playerT)
            return;

        playerTransform = playerT;
        playerRb = playerT.GetComponent<Rigidbody2D>();
        playerSprite = playerT.GetComponent<SpriteRenderer>();
    }

    private IEnumerator PlayTransitionRoutine()
    {
        isPlaying = true;
        hasTriggered = true;
        LockPlayer();

        if (!TryResolveVideoSource(out VideoClip clip, out string url))
        {
            ReleasePlayerLock();
            isPlaying = false;
            playbackCoroutine = null;
            TeleportPlayer();
            yield break;
        }

        if (!TryCreateOverlay(out VideoPlayer videoPlayer))
        {
            ReleasePlayerLock();
            isPlaying = false;
            playbackCoroutine = null;
            TeleportPlayer();
            yield break;
        }

        bool prepared = false;
        bool ended = false;
        bool failed = false;

        void OnPrepared(VideoPlayer _) => prepared = true;
        void OnEnded(VideoPlayer _) => ended = true;
        void OnError(VideoPlayer _, string msg)
        {
            failed = true;
            Debug.LogWarning($"[MirrorEntryTrigger] Video error on '{name}': {msg}");
        }

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.loopPointReached += OnEnded;
        videoPlayer.errorReceived += OnError;

        if (clip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;
        }

        videoPlayer.Prepare();

        float startTime = Time.unscaledTime;
        const float prepareTimeout = 5f;
        while (!prepared && !failed && Time.unscaledTime - startTime < prepareTimeout)
            yield return null;

        if (!failed && prepared)
        {
            videoPlayer.Play();

            float playbackTimeout = 30f;
            if (clip != null && clip.length > 0d)
                playbackTimeout = Mathf.Clamp((float)clip.length + 1f, 3f, 120f);

            float playStart = Time.unscaledTime;
            while (!ended && !failed && Time.unscaledTime - playStart < playbackTimeout)
                yield return null;

            if (!ended && !failed)
                Debug.LogWarning($"[MirrorEntryTrigger] Video playback timeout on '{name}'.");
        }
        else if (!prepared && !failed)
        {
            Debug.LogWarning($"[MirrorEntryTrigger] Video prepare timeout on '{name}'.");
        }

        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.loopPointReached -= OnEnded;
        videoPlayer.errorReceived -= OnError;

        DestroyOverlay();
        ReleasePlayerLock();

        TeleportPlayer();
        PlaceLeftWallAtTriggerRightEdge();

        isPlaying = false;
        playbackCoroutine = null;
    }

    private void PlaceLeftWallAtTriggerRightEdge()
    {
        if (leftWallPrefab == null)
        {
            Debug.LogWarning($"[MirrorEntryTrigger] LeftWall prefab not assigned on '{name}'.");
            return;
        }

        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
            return;

        if (spawnedLeftWall == null)
        {
            spawnedLeftWall = Instantiate(leftWallPrefab);
            spawnedLeftWall.name = leftWallPrefab.name;
        }

        if (!spawnedLeftWall.activeSelf)
            spawnedLeftWall.SetActive(true);

        Bounds triggerBounds = triggerCollider.bounds;
        float centerOffsetX = 0f;
        float centerOffsetY = 0f;
        float halfWidth = 0.05f;

        BoxCollider2D wallCollider = spawnedLeftWall.GetComponent<BoxCollider2D>();
        if (wallCollider != null)
        {
            Vector3 lossyScale = spawnedLeftWall.transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x);
            float scaleY = Mathf.Abs(lossyScale.y);
            if (scaleX < 0.0001f) scaleX = 1f;
            if (scaleY < 0.0001f) scaleY = 1f;

            centerOffsetX = wallCollider.offset.x * lossyScale.x;
            centerOffsetY = wallCollider.offset.y * lossyScale.y;
            halfWidth = Mathf.Max(0.005f, wallCollider.size.x * scaleX * 0.5f);
            wallCollider.enabled = true;
            wallCollider.isTrigger = false;
        }

        Vector3 wallPos = spawnedLeftWall.transform.position;
        wallPos.x = triggerBounds.max.x + leftWallRightPadding + halfWidth - centerOffsetX;
        wallPos.y = triggerBounds.center.y - centerOffsetY;
        spawnedLeftWall.transform.position = wallPos;
    }

    private void TeleportPlayer()
    {
        if (playerTransform == null)
            return;

        Vector3 dest = ResolveDestination();
        if (playerRb != null)
        {
            playerRb.position = new Vector2(dest.x, dest.y);
            playerRb.velocity = Vector2.zero;
        }
        else
        {
            playerTransform.position = dest;
        }
    }

    private Vector3 ResolveDestination()
    {
        if (teleportTarget != null)
            return teleportTarget.position;

        if (findLdtkEntityAtRuntime && hasCachedLdtkTargetPos)
            return cachedLdtkTargetPos;

        if (targetMirror != null && mirrorToMirroredSide)
        {
            Vector3 pos = playerTransform != null ? playerTransform.position : transform.position;
            float mx = targetMirror.transform.position.x;
            float dx = pos.x - mx;
            Vector3 dest = new Vector3(mx - dx, pos.y, pos.z);

            float minDist = 0.5f + safeDistancePadding;
            float mirroredDx = dest.x - mx;
            if (Mathf.Abs(mirroredDx) < minDist)
            {
                float sign = Mathf.Sign(-dx);
                if (Mathf.Approximately(sign, 0f))
                    sign = playerSprite != null && playerSprite.flipX ? -1f : 1f;
                dest.x = mx + sign * minDist;
            }

            return dest;
        }

        return playerTransform != null ? playerTransform.position : transform.position;
    }

    private void LockPlayer()
    {
        if (playerRb == null)
            return;

        cachedConstraints = playerRb.constraints;
        hasCachedConstraints = true;
        playerRb.velocity = Vector2.zero;
        playerRb.angularVelocity = 0f;
        playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void ReleasePlayerLock()
    {
        if (playerRb == null)
            return;

        if (hasCachedConstraints)
            playerRb.constraints = cachedConstraints;

        playerRb.velocity = Vector2.zero;
        playerRb.angularVelocity = 0f;
        hasCachedConstraints = false;
    }

    private bool TryResolveVideoSource(out VideoClip clip, out string url)
    {
        TryAutoAssignClipInEditor();

        clip = entryTransitionClip;
        url = string.Empty;
        if (clip != null)
        {
            missingSourceWarned = false;
            return true;
        }

        string fileName = string.IsNullOrWhiteSpace(entryTransitionVideoFileName)
            ? DefaultVideoFileName
            : entryTransitionVideoFileName.Trim();

        string projectPath = Path.Combine(Application.dataPath, "Textures", "Videos", fileName);
        if (File.Exists(projectPath))
        {
            url = new System.Uri(projectPath).AbsoluteUri;
            missingSourceWarned = false;
            return true;
        }

        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath))
        {
            url = new System.Uri(streamingPath).AbsoluteUri;
            missingSourceWarned = false;
            return true;
        }

        if (!missingSourceWarned)
        {
            Debug.LogWarning(
                $"[MirrorEntryTrigger] Missing video source on '{name}'. Assign Entry Transition Clip or ensure '{fileName}' exists.");
            missingSourceWarned = true;
        }

        return false;
    }

    private bool TryCreateOverlay(out VideoPlayer videoPlayer)
    {
        DestroyOverlay();
        videoPlayer = null;

        GameObject canvasObj = new GameObject("MirrorEntryTransitionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject imgObj = new GameObject("Video");
        imgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = imgObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        RawImage rawImage = imgObj.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.color = Color.white;

        int w = Mathf.Max(2, Screen.width);
        int h = Mathf.Max(2, Screen.height);
        overlayTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        overlayTexture.name = "MirrorEntryTransitionRT";
        overlayTexture.Create();
        rawImage.texture = overlayTexture;

        videoPlayer = canvasObj.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.isLooping = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = overlayTexture;

        overlayCanvas = canvasObj;
        return true;
    }

    private void DestroyOverlay()
    {
        if (overlayCanvas != null)
        {
            if (Application.isPlaying)
                Destroy(overlayCanvas);
            else
                DestroyImmediate(overlayCanvas);

            overlayCanvas = null;
        }

        if (overlayTexture != null)
        {
            if (overlayTexture.IsCreated())
                overlayTexture.Release();

            if (Application.isPlaying)
                Destroy(overlayTexture);
            else
                DestroyImmediate(overlayTexture);

            overlayTexture = null;
        }
    }

    private void CacheLdtkTargetPosition()
    {
        hasCachedLdtkTargetPos = false;
        if (!findLdtkEntityAtRuntime)
            return;

        string iid = string.IsNullOrWhiteSpace(ldtkEnemyIid) ? DefaultLdtkEnemyIid : ldtkEnemyIid.Trim();

        LDtkComponentEntity found = FindLdtkEntityByIid(iid);
        if (found != null)
        {
            cachedLdtkTargetPos = found.transform.position;
            hasCachedLdtkTargetPos = true;
            Debug.Log($"[MirrorEntryTrigger] LDtk entity '{iid}' found at {cachedLdtkTargetPos}");
            return;
        }

        if (!hasCachedLdtkTargetPos && Application.isPlaying)
        {
            Debug.LogWarning($"[MirrorEntryTrigger] LDtk entity '{iid}' not found — falling back to mirrored position.");
        }
    }

    private static LDtkComponentEntity FindLdtkEntityByIid(string iid)
    {
        LDtkComponentEntity[] all = Resources.FindObjectsOfTypeAll<LDtkComponentEntity>();
        foreach (LDtkComponentEntity entity in all)
        {
            if (entity == null)
                continue;

            if (entity.gameObject.scene.IsValid()
                && string.Equals(entity.Iid, iid, System.StringComparison.OrdinalIgnoreCase))
            {
                return entity;
            }
        }

        return null;
    }

    private void TryAutoAssignClipInEditor()
    {
#if UNITY_EDITOR
        if (entryTransitionClip != null)
            return;

        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoAssetPath);
        if (clip != null)
            entryTransitionClip = clip;
#endif
    }

    private void TryAutoAssignLeftWallPrefabInEditor()
    {
#if UNITY_EDITOR
        if (leftWallPrefab != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftWallPrefabPath);
        if (prefab != null)
            leftWallPrefab = prefab;
#endif
    }
}
