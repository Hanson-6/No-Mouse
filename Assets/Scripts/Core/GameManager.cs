using UnityEngine;
using UnityEngine.SceneManagement;
using GestureRecognition.Service;
using GestureRecognition.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Respawn")]
    public Transform respawnPoint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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
        EnsureGestureGameplayBindings();
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

        var panel = FindObjectOfType<GestureDisplayPanel>(true);
        if (panel != null && !panel.gameObject.activeSelf)
            panel.Show();
    }

    public Vector3 GetRespawnPoint()
    {
        return respawnPoint != null ? respawnPoint.position : Vector3.zero;
    }

    public void RestartLevel()
    {
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
