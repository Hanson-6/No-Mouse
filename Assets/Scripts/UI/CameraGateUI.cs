using GestureRecognition.Core;
using GestureRecognition.Service;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Blocks gameplay start until camera + backend are ready.
/// Optionally shows status text for diagnostics and user guidance.
/// </summary>
public class CameraGateUI : MonoBehaviour
{
    private const string PreferredFirstLevelScenePath = "Assets/Scenes/Tutoring.unity";
    private const string SecondaryFirstLevelScenePath = "Assets/Scenes/Tutorial.unity";

    [Header("Optional UI")]
    [SerializeField] private Text statusText;

    [Header("Buttons (auto-found if empty)")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;

    [Header("Scene Settings")]
    [SerializeField] private int firstLevelIndex = 1;

    private CameraRuntimeState _state = CameraRuntimeState.Unknown;
    private bool _recognitionRunning;
    private bool _cameraOccluded;

    public void Configure(Text status, Button newGame, Button cont)
    {
        if (status != null)
            statusText = status;
        if (newGame != null)
            newGameButton = newGame;
        if (cont != null)
            continueButton = cont;
    }

    private void OnEnable()
    {
        GestureEvents.OnCameraStateChanged += OnCameraStateChanged;
        GestureEvents.OnRecognitionStateChanged += OnRecognitionStateChanged;
        GestureEvents.OnCameraOcclusionChanged += OnCameraOcclusionChanged;
    }

    private void OnDisable()
    {
        GestureEvents.OnCameraStateChanged -= OnCameraStateChanged;
        GestureEvents.OnRecognitionStateChanged -= OnRecognitionStateChanged;
        GestureEvents.OnCameraOcclusionChanged -= OnCameraOcclusionChanged;
    }

    private void Start()
    {
        if (newGameButton == null)
            newGameButton = FindButtonByName("NewGameButton");
        if (continueButton == null)
            continueButton = FindButtonByName("ContinueButton");

        _state = GestureService.Instance != null
            ? GestureService.Instance.CameraState
            : CameraRuntimeState.Unknown;
        _recognitionRunning = GestureService.Instance != null && GestureService.Instance.IsRunning;
        _cameraOccluded = GestureService.Instance != null && GestureService.Instance.IsCameraOccluded;

        if (GestureService.Instance != null && !_recognitionRunning)
        {
            GestureService.Instance.StartRecognition();
        }

        RefreshUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && GestureService.Instance != null)
        {
            Debug.Log("[CameraGateUI][Diag] Retry requested via R key.");

            if (GestureService.Instance.IsRunning)
                GestureService.Instance.StopRecognition();

            GestureService.Instance.StartRecognition();
        }
    }

    private void OnCameraStateChanged(CameraRuntimeState state)
    {
        _state = state;
        Debug.Log($"[CameraGateUI][Diag] CameraState changed: {state}");
        RefreshUI();
    }

    private void OnRecognitionStateChanged(bool running)
    {
        _recognitionRunning = running;
        Debug.Log($"[CameraGateUI][Diag] Recognition running: {running}");
        RefreshUI();
    }

    private void OnCameraOcclusionChanged(bool occluded)
    {
        _cameraOccluded = occluded;
        Debug.Log($"[CameraGateUI][Diag] CameraOccluded changed: {occluded}");
        RefreshUI();
    }

    private void RefreshUI()
    {
        bool ready = _state == CameraRuntimeState.Ready && _recognitionRunning && !_cameraOccluded;

        if (newGameButton != null)
            newGameButton.interactable = ready;

        if (continueButton != null && continueButton.gameObject.activeInHierarchy)
            continueButton.interactable = ready;

        if (statusText != null)
            statusText.text = BuildStatusMessage(ready);
    }

    private string BuildStatusMessage(bool ready)
    {
        if (ready)
            return "Camera ready. Press Enter or click to start.";

        if (_cameraOccluded)
            return "Camera appears blocked. Uncover lens, then press R to retry if needed.";

        switch (_state)
        {
            case CameraRuntimeState.Starting:
                return "Initializing camera...";
            case CameraRuntimeState.NoDevice:
                return "No camera detected. Connect or enable webcam, then press R to retry.";
            case CameraRuntimeState.PermissionDenied:
                return "Camera permission denied. Enable permission, then press R to retry.";
            case CameraRuntimeState.BackendUnavailable:
                return "Gesture backend unavailable. Check MediaPipe package setup.";
            case CameraRuntimeState.NoFrame:
                return "Camera not providing frames. Ensure webcam is not blocked by other apps.";
            case CameraRuntimeState.StreamStopped:
                return "Camera stream stopped. Re-enable webcam and press R to retry.";
            default:
                return "Waiting for camera readiness...";
        }
    }

    private static Button FindButtonByName(string buttonName)
    {
        GameObject go = GameObject.Find(buttonName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    // Optional fallback hook for UI button OnClick
    public void StartFirstLevelIfReady()
    {
        if (_state != CameraRuntimeState.Ready || !_recognitionRunning || _cameraOccluded)
        {
            Debug.LogWarning($"[CameraGateUI][Diag] Start blocked: state={_state} running={_recognitionRunning} occluded={_cameraOccluded}.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(ResolveFirstLevelBuildIndex());
    }

    // Optional fallback hook for UI button OnClick
    public void ContinueIfReady()
    {
        if (_state != CameraRuntimeState.Ready || !_recognitionRunning || _cameraOccluded)
        {
            Debug.LogWarning($"[CameraGateUI][Diag] Continue blocked: state={_state} running={_recognitionRunning} occluded={_cameraOccluded}.");
            return;
        }

        Time.timeScale = 1f;

        if (!SaveManager.ContinueFromLatestSnapshot())
            Debug.LogWarning("[CameraGateUI] Continue 失败：未找到可恢复的 session/checkpoint 存档。");
    }

    private int ResolveFirstLevelBuildIndex()
    {
        int preferredIndex = SceneUtility.GetBuildIndexByScenePath(PreferredFirstLevelScenePath);
        if (preferredIndex >= 0)
            return preferredIndex;

        int secondaryIndex = SceneUtility.GetBuildIndexByScenePath(SecondaryFirstLevelScenePath);
        if (secondaryIndex >= 0)
            return secondaryIndex;

        Debug.LogWarning($"[CameraGateUI] Neither '{PreferredFirstLevelScenePath}' nor '{SecondaryFirstLevelScenePath}' is in Build Settings. Fallback to serialized firstLevelIndex={firstLevelIndex}.");
        return firstLevelIndex;
    }
}
