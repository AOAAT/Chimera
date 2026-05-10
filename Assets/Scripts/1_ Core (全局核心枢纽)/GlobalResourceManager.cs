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

    public int Materials = 0;
    public int DaysSurvived = 1;

    // 👇【核心新增】：产能上限（玩家的可用电量总额）
    [Header("=== 电网枢纽 ===")]
    public int MaxPowerCapacity = 100;

    [Header("=== 失败设定 ===")]
    [Tooltip("当理智归零时触发的特定失败事件")]
    public EventNodeSO SanityCollapseEvent;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==========================================
    // 💡 产能与耗能核心计算
    // ==========================================
    // 计算当前【所有已部署在战场上】的机甲，总共吃了多少电？
    public int GetTotalUsedPower()
    {
        int usedPower = 0;
        if (PlayerInventoryManager.Instance != null && PlayerInventoryManager.Instance.HangarUnits != null)
        {
            // 遍历机库，只算那些被玩家拖到战场上（IsDeployed == true）的机甲
            foreach (var unit in PlayerInventoryManager.Instance.HangarUnits)
            {
                if (unit != null && unit.IsDeployed)
                {
                    usedPower += CalculateUnitPowerCost(unit);
                }
            }
        }
        return usedPower;
    }

    // 辅助：计算单台机甲的总耗电量 (底盘耗电 + 所有组件耗电)
    public int CalculateUnitPowerCost(SavedUnitProfile unit)
    {
        if (unit == null || unit.ChassisData == null) return 0;

        float power = PlayerInventoryManager.GetStatValue(unit.ChassisData.BaseStats, StatType.PowerCost);

        foreach (string compID in unit.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) power += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
            }
        }
        return Mathf.RoundToInt(power);
    }

    // 预留接口：未来可以通过事件或科技树增加产能上限
    public void ModifyMaxPower(int amount)
    {
        MaxPowerCapacity += amount;
        Debug.Log($"【电网扩容】最大产能增加 {amount}，当前总产能: {MaxPowerCapacity}");
        OnResourceChanged?.Invoke();
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
    public void ModifyMaterials(int amount)
    {
        Materials = Mathf.Max(0, Materials + amount);
        OnResourceChanged?.Invoke();
    }

    public void AdvanceDay()
    {
        DaysSurvived++;
        OnResourceChanged?.Invoke();
    }

    private void ExecuteGameOverProtocol()
    {
        Debug.Log("<color=red>【致命警告】</color> 理智归零。执行强制关机协议...");

        // 1. 👇【核心加固】：强行切断战斗导演的 UI 链路
        if (CombatDirector.Instance != null)
        {
            // 彻底隐藏结算面板，防止玩家点击“返回地图”
            if (CombatDirector.Instance.SettlementPanel != null)
                CombatDirector.Instance.SettlementPanel.SetActive(false);

            // 隐藏部署界面
            if (CombatDirector.Instance.CombatUIPanel != null)
                CombatDirector.Instance.CombatUIPanel.SetActive(false);

            // 清理战场实体
            CombatDirector.Instance.PerformFullCleanup();
        }

        // 2. 隐藏大地图和其他 UI
        if (MapManager.Instance != null && MapManager.Instance.MapUIPanel != null)
            MapManager.Instance.MapUIPanel.SetActive(false);
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.GlobalPauseButton != null)
        {
            PauseMenuUI.Instance.GlobalPauseButton.SetActive(false);
        }
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