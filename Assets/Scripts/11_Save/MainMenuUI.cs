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
        // --- 👇【核心新增】：唤醒主菜单音乐 ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SwitchState(MusicState.MainMenu);
        }
        // ----------------------------------

        if (NewGameButton != null) NewGameButton.onClick.AddListener(StartNewGame);
        if (QuitButton != null) QuitButton.onClick.AddListener(QuitGame);
    }

    private void StartNewGame()
    {
        // --- 👇【关键加固】：在加载场景前，手动切断单例连接 ---
        // 这样新场景加载时，它们的 Awake 会发现 Instance 为 null，从而正常初始化。
        if (MapManager.Instance != null) Destroy(MapManager.Instance.gameObject);
        if (RunManager.Instance != null) Destroy(RunManager.Instance.gameObject);
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