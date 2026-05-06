using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("=== 按钮引用 ===")]
    public Button ContinueButton;
    public GameObject SettingsPanel;
    public GameObject TutorialPanel;

    private void Start()
    {
        // 检查是否有档，没有档就把“载入”按钮灰掉
        if (ContinueButton != null)
        {
            ContinueButton.interactable = SaveManager.Instance.HasSaveFile();
        }

        if (SettingsPanel != null) SettingsPanel.SetActive(false);
        if (TutorialPanel != null) TutorialPanel.SetActive(false);
    }

    public void OnClickNewGame()
    {
        // 如果有旧档，可以弹个窗，这里直接开新局
        MapManager.Instance.StartNewExpedition();
        // 隐藏主菜单 UI
        this.gameObject.SetActive(false);
    }

    public void OnClickContinue()
    {
        if (SaveManager.Instance.LoadGame())
        {
            // 回到地图
            if (MapManager.Instance != null)
            {
                MapManager.Instance.MapUIPanel.SetActive(true);
                FindObjectOfType<MapVisualizer>()?.RefreshAllVisuals();
            }
            this.gameObject.SetActive(false);
        }
    }

    public void OnClickExit()
    {
        Application.Quit();
    }

    // 设置音量 (简易版)
    public void SetGlobalVolume(float val)
    {
        AudioListener.volume = val;
    }
}