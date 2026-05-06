using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance;
    public GameObject Panel;

    private void Awake() { Instance = this; Panel.SetActive(false); }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        bool isPaused = !Panel.activeSelf;
        Panel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f; // 暂停游戏时间
    }

    public void OnClickSaveAndMenu()
    {
        SaveManager.Instance.SaveGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // 重启当前场景回到主菜单状态
    }

    public void OnClickSaveAndExit()
    {
        SaveManager.Instance.SaveGame();
        Application.Quit();
    }
}
