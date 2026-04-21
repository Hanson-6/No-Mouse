using UnityEngine;
using UnityEngine.SceneManagement;
using GestureRecognition.Service;
using GestureRecognition.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Respawn")]
    public Transform respawnPoint;

    private Vector3 initialRespawnPosition;
    private bool hasInitialRespawnPosition;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheInitialRespawnPoint();
        ApplyCheckpointRespawnIfAvailable();
        MovePlayerToRespawnIfNeeded();

        if (FindObjectOfType<GestureService>() == null)
        {
            var gestureServiceGo = new GameObject("GestureService");
            gestureServiceGo.AddComponent<GestureService>();
        }

        EnsureGestureGameplayBindings();
    }

    void Start()
    {
        EnsureGestureGameplayBindings();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheInitialRespawnPoint();
        ApplyCheckpointRespawnIfAvailable();
        MovePlayerToRespawnIfNeeded();
        EnsureGestureGameplayBindings();
    }

    private void CacheInitialRespawnPoint()
    {
        if (respawnPoint == null)
        {
            hasInitialRespawnPosition = false;
            return;
        }

        initialRespawnPosition = respawnPoint.position;
        hasInitialRespawnPosition = true;
    }

    private void ApplyCheckpointRespawnIfAvailable()
    {
        if (respawnPoint == null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (GameData.TryGetCheckpoint(activeScene.buildIndex, activeScene.path, out Vector3 checkpointPos))
            respawnPoint.position = checkpointPos;
    }

    private void MovePlayerToRespawnIfNeeded()
    {
        if (respawnPoint == null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!GameData.TryGetCheckpoint(activeScene.buildIndex, activeScene.path, out _))
            return;

        var player = FindObjectOfType<PlayerController>();
        if (player == null)
            return;

        player.transform.position = respawnPoint.position;

        var playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
            playerRb.velocity = Vector2.zero;
    }

    private void EnsureGestureGameplayBindings()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        var gestureBridge = FindObjectOfType<GestureInputBridge>();
        if (gestureBridge == null)
        {
            var go = new GameObject("GestureInputBridge");
            gestureBridge = go.AddComponent<GestureInputBridge>();
        }
        gestureBridge.SetPlayer(player);

        var invulnerableBodyController = FindObjectOfType<InvulnerableBodyController>();
        if (invulnerableBodyController == null)
        {
            var go = new GameObject("InvulnerableBodyController");
            invulnerableBodyController = go.AddComponent<InvulnerableBodyController>();
        }
        invulnerableBodyController.SetPlayer(player);

        var spiritHand = FindObjectOfType<SpiritHandDisplay>();
        if (spiritHand == null)
        {
            var go = new GameObject("SpiritHand");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            spiritHand = go.AddComponent<SpiritHandDisplay>();
            spiritHand.SetPlayer(player);
        }
        else
        {
            spiritHand.SetPlayer(player);
        }

        var darkVision = FindObjectOfType<DarkVisionController>();
        if (darkVision == null)
        {
            var go = new GameObject("DarkVisionController");
            darkVision = go.AddComponent<DarkVisionController>();
            darkVision.SetPlayer(player);
        }
        else
        {
            darkVision.SetPlayer(player);
        }

        var panel = FindObjectOfType<GestureDisplayPanel>(true);
        if (panel != null && !panel.gameObject.activeSelf)
            panel.Show();
    }

    public Vector3 GetRespawnPoint()
    {
        return respawnPoint != null ? respawnPoint.position : Vector3.zero;
    }

    public void SetCheckpoint(Transform checkpointTransform)
    {
        if (checkpointTransform == null || respawnPoint == null)
            return;

        Vector3 checkpointPos = checkpointTransform.position;
        respawnPoint.position = checkpointPos;

        Scene activeScene = SceneManager.GetActiveScene();
        GameData.SetCheckpoint(activeScene.buildIndex, activeScene.path, checkpointPos);
    }

    public void ResetRespawnToInitial()
    {
        if (!hasInitialRespawnPosition || respawnPoint == null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        respawnPoint.position = initialRespawnPosition;
        GameData.ClearCheckpoint(activeScene.buildIndex, activeScene.path);
    }

    public void RestartLevel()
    {
        GameData.ClearDarkMode();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.Log("No more levels!");
    }
}
