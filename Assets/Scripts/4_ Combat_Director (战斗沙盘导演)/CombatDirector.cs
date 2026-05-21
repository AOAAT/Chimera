using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    [Header("=== 运行时单位注册表 ===")]
    public static List<DamageReceiver> ActiveEnemies = new List<DamageReceiver>();
    public static List<DamageReceiver> ActivePlayerUnits = new List<DamageReceiver>();
    // --- 请在 CombatDirector.cs 类中添加这些字段和方法 ---

    [Header("=== 导航面板引用 ===")]
    public GameObject NavigationPanel; // 指向你场景中的主菜单/导航条

    public void SetNavigationVisibility(bool isVisible)
    {
        if (NavigationPanel != null)
        {
            NavigationPanel.SetActive(isVisible);
        }
    }
    // 在 RTS 模式下，战斗默认永远处于激活状态，或者由全局战争状态控制
    public bool IsCombatActive { get; private set; } = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 彻底简化清理逻辑，只负责抹除列表和销毁实体
    public void PerformFullCleanup()
    {
        foreach (var unit in ActivePlayerUnits.Concat(ActiveEnemies).ToList())
        {
            if (unit != null) Destroy(unit.gameObject);
        }

        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        // 清理对象池（子弹、飘字等）
        SimplePool.ClearPool();
        Debug.Log("<color=yellow>【系统】</color> 战场已完全重置。");
    }
    // --- 请添加到 CombatDirector.cs 类中 ---
    public void FullResetBeforeExit()
    {
        // 直接复用我们写好的全量清理逻辑
        PerformFullCleanup();

        // 确保时间流速恢复正常，防止主菜单卡死
        Time.timeScale = 1f;
    }
    // 设置战斗开关（例如进入暂停或大地图时调用）
    public void SetCombatActive(bool active)
    {
        IsCombatActive = active;
    }
    public void ExecuteReturnToMap()
    {
        // 之前这里会呼叫 MapManager 回到地图，现在我们把它改成“原地待命”
        Debug.Log("<color=cyan>【隔离模式】</color> 战斗结束，地图系统已挂起，留在当前战场。");

        // 仅仅执行物理层清理，不执行 UI 切换
        PerformFullCleanup();

        // 如果你想测试完直接回机库，可以改为：
        // if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.OpenHangar();
    }
}