using System;
using UnityEngine;
using System.Linq; // 用于查询机库

public class GlobalResourceManager : MonoBehaviour
{
    public static GlobalResourceManager Instance;

    public event Action OnResourceChanged;

    [Header("=== 全局状态 ===")]
    public int MaxSAN = 100;
    public int CurrentSAN = 100;

    public int DaysSurvived = 1;


    [Header("=== 失败设定 ===")]
    [Tooltip("当理智归零时触发的特定失败事件")]
    public EventNodeSO SanityCollapseEvent;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==========================================
    // (保留之前的 SAN、Material、Days 逻辑)
    // ==========================================
    public void ModifySAN(int amount)
    {
        CurrentSAN = Mathf.Clamp(CurrentSAN + amount, 0, MaxSAN);
        OnResourceChanged?.Invoke();

        // --- 👇【核心重构：失败判定】---
        if (CurrentSAN <= 0)
        {
            ExecuteGameOverProtocol();
        }
    }

    public void AdvanceDay()
    {
        DaysSurvived++;
        OnResourceChanged?.Invoke();
    }

    // --- 请找到 GlobalResourceManager.cs 的 ExecuteGameOverProtocol 方法并替换 ---
    private void ExecuteGameOverProtocol()
    {
        Debug.Log("<color=red>【致命警告】</color> 理智归零。执行强制关机协议...");

        // 1. 清理战斗物理层
        if (CombatDirector.Instance != null)
        {
            // 彻底清理战场实体、容器和池子
            CombatDirector.Instance.PerformFullCleanup();
        }

        // 2. 隐藏大地图和其他 UI (假设这些面板依然存在)
        if (MapManager.Instance != null && MapManager.Instance.MapUIPanel != null)
            MapManager.Instance.MapUIPanel.SetActive(false);

        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.GlobalPauseButton != null)
            PauseMenuUI.Instance.GlobalPauseButton.SetActive(false);

        // 3. 唤醒谢幕事件
        if (SanityCollapseEvent != null)
        {
            EventDirector.Instance.PlayEvent(SanityCollapseEvent);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}