using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance;

    [Header("=== UI 面板 ===")]
    public GameObject PausePanel;

    [Header("=== 按钮引用 ===")]
    public Button ContinueButton;
    public Button MainMenuButton;
    public Button QuitButton;

    // 👇【核心新增】：用于控制全局“暂停按钮”的显隐（可选）
    public GameObject GlobalPauseButton;

    private bool isPaused = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (PausePanel != null) PausePanel.SetActive(false);
    }

    private void Start()
    {
        if (ContinueButton != null) ContinueButton.onClick.AddListener(ResumeGame);
        if (MainMenuButton != null) MainMenuButton.onClick.AddListener(BackToMainMenu);
        if (QuitButton != null) QuitButton.onClick.AddListener(QuitGame);
    }

    private void Update()
    {
        // 键盘 Esc 逻辑保持不变
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // ==========================================
    // 🚀 核心接口：一键切换暂停状态
    // ==========================================
    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }
    public void PauseGame()
    {
        if (isPaused) return;

        isPaused = true;
        if (PausePanel != null) PausePanel.SetActive(true);

        // 核心：暂停物理和逻辑模拟
        Time.timeScale = 0f;

        // 声音反馈
        if (MusicManager.Instance != null) MusicManager.Instance.SetImmersionMode(true);

        // 隐藏全局暂停按钮，防止 UI 重叠
        if (GlobalPauseButton != null) GlobalPauseButton.SetActive(false);

        Debug.Log("<color=yellow>【系统】</color> 游戏已暂停。");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        if (MusicManager.Instance != null) MusicManager.Instance.SetImmersionMode(false);

        // 恢复显示全局暂停按钮
        if (GlobalPauseButton != null) GlobalPauseButton.SetActive(true);

        Debug.Log("<color=yellow>【系统】</color> 逻辑继续。");
    }
    private void BackToMainMenu()
    {
        Debug.Log("<color=orange>【系统】</color> 正在执行离场清理并返回主菜单...");

        // --- 👇【核心新增】：先大扫除，再切场景 ---
        if (CombatDirector.Instance != null)
        {
            CombatDirector.Instance.FullResetBeforeExit();
        }

        // 额外确保时间流速正常
        Time.timeScale = 1f;

        // 卸载当前场景，加载主菜单（Index 0）
        SceneManager.LoadScene(0);
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