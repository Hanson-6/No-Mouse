using UnityEngine;
using UnityEngine.SceneManagement;

public class EndPoint : MonoBehaviour
{
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
        GameData.CurrentLevel = SceneManager.GetActiveScene().buildIndex;

        if (animator != null)
            animator.SetBool("Pressed", true);

        Invoke(nameof(LoadLevelComplete), 0.5f); // 短暂停顿让动画播一帧
    }

    void LoadLevelComplete()
    {
        SceneManager.LoadScene("LevelComplete");
    }
}
