using System;
using UnityEngine;

public class GlobalResourceManager : MonoBehaviour
{
    public static GlobalResourceManager Instance;
    public event Action OnResourceChanged;

    [Header("=== 局内资源 (预留接口) ===")]
    // 未来将在此处添加：锈蚀零件、搏动生物质、虚空原质
    public int DaysSurvived = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AdvanceDay()
    {
        DaysSurvived++;
        OnResourceChanged?.Invoke();
    }

    /// <summary>
    /// 🌟 失败判定重构：未来可由“基地被毁”触发
    /// </summary>
    public void ExecuteGameOverProtocol()
    {
        Debug.Log("<color=red>【核心崩溃】</color> 基地失守。执行强制关机协议...");

        if (CombatDirector.Instance != null)
            CombatDirector.Instance.PerformFullCleanup();

        // 直接返回主菜单
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}