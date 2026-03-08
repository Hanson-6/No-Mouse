using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles Main Menu button logic.
/// Attach to MenuManager. Buttons are wired automatically in Awake by name,
/// or you can assign them manually in the Inspector.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Button References (auto-found if left empty)")]
    public Button startButton;
    public Button quitButton;

    void Awake()
    {
        if (startButton == null)
            startButton = FindButtonByName("StartButton");

        if (quitButton == null)
            quitButton = FindButtonByName("QuitButton");

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    static Button FindButtonByName(string buttonName)
    {
        var go = GameObject.Find(buttonName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    // ── Public methods (also usable as persistent Inspector listeners) ──────

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Level1");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
