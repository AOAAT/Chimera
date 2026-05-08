using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



// ==========================================
// 1. 底盘实体的“身份证”
// ==========================================
[Serializable]
public class InstancedChassis
{
    public string InstanceID;
    public ChassisDataSO BaseData;
    public string EquippedUnitID;

    public InstancedChassis(ChassisDataSO data)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        EquippedUnitID = string.Empty;
    }

    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}

// ==========================================
// 2. 散件实体的“身份证” (引入等级)
// ==========================================
[Serializable]
public class InstancedComponent
{
    public string InstanceID;
    public ComponentDataSO BaseData;
    public string EquippedUnitID;
    public int CurrentLevel = 1;

    public InstancedComponent(ComponentDataSO data, int level)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        CurrentLevel = level;
        EquippedUnitID = string.Empty;
    }

    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}

// ==========================================
// 3. 机甲的持久化档案
// ==========================================
[Serializable]
public class SavedUnitProfile
{
    public string UnitID;
    public string UnitName;
    public ChassisDataSO ChassisData;

    [Header("战损记录")]
    public float CurrentHP;
    public float CurrentAP;

    public bool IsDeployed = false;

    public List<int> SlotIndices = new List<int>();
    public List<string> EquippedComponentIDs = new List<string>();

    public string ChassisInstanceID;

    public SavedUnitProfile(InstancedChassis chassisInstance, string name = "未命名机甲")
    {
        UnitID = Guid.NewGuid().ToString();
        UnitName = name;

        ChassisData = chassisInstance.BaseData;
        ChassisInstanceID = chassisInstance.InstanceID;

        CurrentHP = PlayerInventoryManager.GetStatValue(ChassisData.BaseStats, StatType.AddedHP);
        CurrentAP = PlayerInventoryManager.GetStatValue(ChassisData.BaseStats, StatType.AddedAP);
    }
}

// ==========================================
// 4. 玩家资产总管
// ==========================================
public class PlayerInventoryManager : MonoBehaviour
{
    public static PlayerInventoryManager Instance;
    public event Action OnInventoryChanged;

    [Header("=== 核心资产 ===")]
    public int MaxUnitSlots = 8;
    public SavedUnitProfile[] HangarUnits;

    [Header("=== 实体仓库 ===")]
    public List<InstancedChassis> ChassisInventory = new List<InstancedChassis>();
    public List<InstancedComponent> ComponentInventory = new List<InstancedComponent>();

    [Header("=== 游戏全局图纸库 (抽卡池 Database) ===")]
    public List<ChassisDataSO> AllChassisDatabase = new List<ChassisDataSO>();
    public List<ComponentDataSO> AllComponentDatabase = new List<ComponentDataSO>();

    [Header("=== 测试作弊专用 ===")]
    public ChassisDataSO DebugChassisBlueprint;
    public List<ComponentDataSO> DebugComponentBundle = new List<ComponentDataSO>();

    [Tooltip("预设的机甲名称库，玩家新建机甲时会从中随机抽取")]
    public List<string> DefaultNamePool = new List<string> {
    "苍穹破裂者", "铁肺", "苦难摇篮", "西西弗斯", "黑匣子",
    "零号病人", "柴油之心", "锈蚀审判", "无声呐喊", "利维坦",
    "赤红风暴","小小","哈基米","故障机器人","危险流浪者","高达"
};

    // 运行时剩余可用的名称
    private List<string> runtimeAvailableNames;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 洗库操作 1：给旧底盘发身份证
        foreach (var c in ChassisInventory)
        {
            if (string.IsNullOrEmpty(c.InstanceID))
            {
                c.InstanceID = Guid.NewGuid().ToString();
                c.EquippedUnitID = string.Empty;
            }
        }

        // 洗库操作 2：给旧零件发身份证，并【强行肃清 0 级黑户】！
        foreach (var c in ComponentInventory)
        {
            if (string.IsNullOrEmpty(c.InstanceID))
            {
                c.InstanceID = Guid.NewGuid().ToString();
                c.EquippedUnitID = string.Empty;
            }

            // 👇【史诗级修复】：只要你是 0 级，就强制给你变成 1 级！
            if (c.CurrentLevel <= 0)
            {
                c.CurrentLevel = 1;
            }
        }
        HangarUnits = new SavedUnitProfile[MaxUnitSlots];

        InitNamePool();
}
  
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (DebugChassisBlueprint != null) AddChassisToInventory(DebugChassisBlueprint);
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (DebugComponentBundle != null && DebugComponentBundle.Count > 0)
            {
                foreach (var blueprint in DebugComponentBundle)
                {
                    if (blueprint != null) AddComponentToInventory(blueprint); // 默认发放 1 级
                }
            }
        }
    }
    public string GetNextAvailableName()
    {
        if (runtimeAvailableNames == null) InitNamePool();

        if (runtimeAvailableNames.Count == 0)
        {
            // 如果池子抽干了，返回一个带编号的保底名
            return $"未知型号-{UnityEngine.Random.Range(100, 999)}";
        }

        int randomIndex = UnityEngine.Random.Range(0, runtimeAvailableNames.Count);
        string selectedName = runtimeAvailableNames[randomIndex];

        // 从池子中移除，防止重复
        runtimeAvailableNames.RemoveAt(randomIndex);
        return selectedName;
    }
    public static float GetStatValue(List<StatEntry> stats, StatType targetStat)
    {
        if (stats == null) return 0f;
        foreach (var stat in stats) if (stat.StatID == targetStat) return stat.Value;
        return 0f;
    }
    public void ReturnNameToPool(string oldName)
    {
        if (DefaultNamePool.Contains(oldName) && !runtimeAvailableNames.Contains(oldName))
        {
            runtimeAvailableNames.Add(oldName);
        }
    }
    public List<InstancedComponent> GetFilteredInventory(ComponentType requiredType, SubTag? requiredTag = null)
    {
        var query = ComponentInventory.Where(c => c.BaseData.Type == requiredType);
        if (requiredTag.HasValue) query = query.Where(c => c.BaseData.BaseSubTags.Contains(requiredTag.Value));

        return query.OrderBy(c => c.IsEquipped).ThenBy(c => c.BaseData.ComponentName).ToList();
    }

    public bool ValidateHPBeforeUnequip(SavedUnitProfile unit, InstancedComponent componentToRemove, InstancedComponent componentToEquip = null)
    {
        float currentHP = unit.CurrentHP;

        if (componentToRemove != null)
        {
            var lvData = componentToRemove.BaseData.GetLevelData(componentToRemove.CurrentLevel);
            if (lvData != null) currentHP -= GetStatValue(lvData.Stats, StatType.AddedHP);
        }

        if (componentToEquip != null)
        {
            var lvData = componentToEquip.BaseData.GetLevelData(componentToEquip.CurrentLevel);
            if (lvData != null) currentHP += GetStatValue(lvData.Stats, StatType.AddedHP);
        }

        if (currentHP <= 0) return false;
        return true;
    }
    private void InitNamePool()
    {
        runtimeAvailableNames = new List<string>(DefaultNamePool);
    }
    public void AddComponentToInventory(ComponentDataSO so, int level = 1)
    {
        var newItem = new InstancedComponent(so, level);
        ComponentInventory.Add(newItem);
        OnInventoryChanged?.Invoke();
    }

    public void AddChassisToInventory(ChassisDataSO so)
    {
        var newItem = new InstancedChassis(so);
        ChassisInventory.Add(newItem);
        OnInventoryChanged?.Invoke();
    }

    // ==========================================
    // 🧠 同源合成中枢 (Merge System)
    // ==========================================
    public bool CanMerge(InstancedComponent a, InstancedComponent b)
    {
        if (a == null || b == null) return false;
        if (a.InstanceID == b.InstanceID) return false; // 防止自己跟自己合成
        if (a.BaseData.ComponentBaseID != b.BaseData.ComponentBaseID) return false; // 必须是同一种武器
        if (a.CurrentLevel != b.CurrentLevel) return false; // 必须同星级
        if (a.CurrentLevel >= a.BaseData.LevelMatrix.Count) return false; // 严禁超越等级上限
        return true;
    }

    public InstancedComponent ExecuteMerge(InstancedComponent a, InstancedComponent b)
    {
        if (!CanMerge(a, b)) return null;

        ComponentInventory.Remove(a);
        ComponentInventory.Remove(b);

        InstancedComponent upgradedItem = new InstancedComponent(a.BaseData, a.CurrentLevel + 1);
        ComponentInventory.Add(upgradedItem);

        Debug.Log($"<color=#FF00FF>【车间电焊】</color> 成功将两个 Lv{a.CurrentLevel} 的 [{a.BaseData.ComponentName}] 合成为 Lv{upgradedItem.CurrentLevel}！");
        OnInventoryChanged?.Invoke();
        return upgradedItem;
    }
    // --- 请在 PlayerInventoryManager.cs 中追加此方法 ---
    public void DismantleUnit(int slotIndex)
    {
        SavedUnitProfile unit = HangarUnits[slotIndex];
        if (unit == null) return;

        Debug.Log($"<color=orange>【机甲解体】</color> 正在拆解机甲: {unit.UnitName}...");

        // 1. 释放底盘
        var chassis = ChassisInventory.Find(c => c.InstanceID == unit.ChassisInstanceID);
        if (chassis != null)
        {
            chassis.EquippedUnitID = string.Empty;
        }

        // 2. 释放所有挂载的零件
        foreach (string compID in unit.EquippedComponentIDs)
        {
            var comp = ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null)
            {
                comp.EquippedUnitID = string.Empty;
            }
        }

        // 3. 将机甲名称归还池子 (可选)
        ReturnNameToPool(unit.UnitName);

        // 4. 清空车位
        HangarUnits[slotIndex] = null;

        // 5. 广播库存变动，让仓库、详情页等自动刷新
        ForceTriggerInventoryEvent();

        Debug.Log($"<color=yellow>【解体成功】</color> 底盘与组件已回归原始库存。");
    }
    public void ForceTriggerInventoryEvent()
    {
        OnInventoryChanged?.Invoke();
    }
}