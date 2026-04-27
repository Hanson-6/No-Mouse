using UnityEngine;
using UnityEngine.SceneManagement;

public class EndPoint : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip winSound;

    private bool triggered = false;
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        GameData.FinalTime = GameData.CurrentTimer;
        GameData.CurrentLevel = SceneManager.GetActiveScene().buildIndex;

        if (GameManager.Instance != null)
            GameManager.Instance.ResetRespawnToInitial();
        else
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameData.ClearCheckpoint(activeScene.buildIndex, activeScene.path);
        }

        GameData.ClearDarkMode();

        if (animator != null)
            animator.SetBool("Pressed", true);

        if (winSound != null) AudioSource.PlayClipAtPoint(winSound, transform.position);

        var player = other.GetComponent<PlayerController>();
        if (player != null) player.AutoWalk(player.FacingRight ? 1f : -1f);

        Invoke(nameof(LoadLevelComplete), 1.5f);
    }

    void LoadLevelComplete()
    {
        SceneManager.LoadScene("LevelComplete");
    }
}
