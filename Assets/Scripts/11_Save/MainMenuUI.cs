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
        // 清理所有可能残留的单例（防止回主菜单后再次进入产生的Bug）
        ClearAllManagers();

        if (NewGameButton != null) NewGameButton.onClick.AddListener(StartNewGame);
        if (QuitButton != null) QuitButton.onClick.AddListener(QuitGame);

        // 载入功能暂时禁用
        if (LoadGameButton != null) LoadGameButton.interactable = false;
    }

    private void StartNewGame()
    {
        Debug.Log("<color=cyan>【系统】</color> 正在重载逻辑序列，初始化新进度...");

        // 场景跳转：1 代表主游戏场景，0 代表主菜单
        // 确保 Build Settings 里 Scene_MainGame 的索引是 1
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

    private void ClearAllManagers()
    {
        // 这一步是为了防止那些开启了 DontDestroyOnLoad 的脚本在大厅残留
        // 强制销毁旧的管理器实例
        var managers = GameObject.FindObjectsOfType<GameObject>();
        foreach (var m in managers)
        {
            // 只要名字里带 Manager 或 Director 且不是当前场景物体的，标记清理
            if (m.name.Contains("Manager") || m.name.Contains("Director"))
            {
                // 注意：这里不销毁 MainMenu 里的 UI，只销毁持久化的逻辑物体
                if (m.transform.parent == null) // 通常单例都在根节点
                {
                    // Destroy(m); // 暂时注释，如果发现单例冲突再启用
                }
            }
        }
    }
}