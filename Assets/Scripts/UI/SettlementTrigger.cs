using UnityEngine;
using UnityEngine.SceneManagement;

public class SettlementTrigger : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip winSound;

    private bool triggered = false;

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
            GameData.ClearCheckpoint(SceneManager.GetActiveScene().buildIndex);

        GameData.ClearDarkMode();

        if (winSound != null)
            AudioSource.PlayClipAtPoint(winSound, transform.position);

        var player = other.GetComponent<PlayerController>();
        if (player != null) player.AutoWalk(player.FacingRight ? 1f : -1f);

        Invoke(nameof(LoadLevelComplete), 1.5f);
    }

    void LoadLevelComplete()
    {
        SceneManager.LoadScene("LevelComplete");
    }
}
