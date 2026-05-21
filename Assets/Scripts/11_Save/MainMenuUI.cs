using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("=== 按钮引用 ===")]
    public Button NewGameButton;
    public Button LoadGameButton; // 暂时预留
    public Button QuitButton;

    private void Start()
    {
        // 唤醒主菜单音乐
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SwitchState(MusicState.MainMenu);
        }

        if (NewGameButton != null) NewGameButton.onClick.AddListener(StartNewGame);

        if (QuitButton != null) QuitButton.onClick.AddListener(QuitGame);
    }

    private void StartNewGame()
    {

        if (MapManager.Instance != null) Destroy(MapManager.Instance.gameObject);
        if (PlayerInventoryManager.Instance != null) Destroy(PlayerInventoryManager.Instance.gameObject);
        if (GlobalResourceManager.Instance != null) Destroy(GlobalResourceManager.Instance.gameObject);
        if (CombatDirector.Instance != null) Destroy(CombatDirector.Instance.gameObject);

        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        Debug.Log("【系统】执行退出协议，安全关闭...");

#if UNITY_EDITOR
        // 如果在编辑器里，点击退出会停止播放模式，而不是关掉整个 Unity
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 打包后，这就是真实的关闭程序
        Application.Quit();
#endif
    }

}