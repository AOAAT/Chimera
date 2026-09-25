using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    [Header("=== 运行时单位注册表 ===")]
    public static List<DamageReceiver> ActiveEnemies = new List<DamageReceiver>();
    public static List<DamageReceiver> ActivePlayerUnits = new List<DamageReceiver>();
    // 场景中的战斗独立运行，不等待关卡推进。
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
    // 控制场景中的战斗模拟。
    public void SetCombatActive(bool active)
    {
        IsCombatActive = active;
    }
}
