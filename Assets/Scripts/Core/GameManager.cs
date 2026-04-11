using UnityEngine;
using UnityEngine.SceneManagement;
using GestureRecognition.Service;

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
