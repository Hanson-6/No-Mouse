using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelComplete : MonoBehaviour
{
    void Start()
    {
        Time.timeScale = 1f;
        EnsureCanvasScale();

        var nextBtn = GameObject.Find("NextLevelButton")?.GetComponent<Button>();
        if (nextBtn != null) nextBtn.onClick.AddListener(NextLevel);

        var menuBtn = GameObject.Find("MainMenuButton")?.GetComponent<Button>();
        if (menuBtn != null) menuBtn.onClick.AddListener(GoToMainMenu);
    }

    void NextLevel()
    {
        int next = GameData.CurrentLevel + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(0); // 没有下一关，回主菜单
    }

    void GoToMainMenu()
    {
        SceneManager.LoadScene(0);
    }

    private static void EnsureCanvasScale()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        Vector3 scale = canvas.transform.localScale;
        if (Mathf.Abs(scale.x) < 0.001f && Mathf.Abs(scale.y) < 0.001f)
        {
            canvas.transform.localScale = Vector3.one;
            Debug.Log("[LevelComplete][Diag] Canvas scale was zero; restored to (1,1,1).");
        }
    }
}
