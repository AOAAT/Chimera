using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ==========================================
// 1. 实物档案与堆叠结构
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

[Serializable]
public class InstancedComponent
{
    public string InstanceID;
    public ComponentDataSO BaseData;
    public string EquippedUnitID;
    public int CurrentLevel = 1;
    public List<string> SocketedAccessoryIDs = new List<string>();

    public InstancedComponent(ComponentDataSO data, int level)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        CurrentLevel = level;
        EquippedUnitID = string.Empty;
        SocketedAccessoryIDs = new List<string>();
    }
    public int GetMaxSockets()
    {
        if (BaseData == null) return 0;
        var lvData = BaseData.GetLevelData(this.CurrentLevel);
        return lvData != null ? lvData.MaxSocketCount : 1;
    }
    public bool HasEmptySocket() => SocketedAccessoryIDs.Count < GetMaxSockets();
    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}

[Serializable]
public class InstancedAccessory
{
    public string InstanceID;
    public AccessoryDataSO BaseData;
    public string ParentComponentID = string.Empty;
    public InstancedAccessory(AccessoryDataSO data)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        ParentComponentID = string.Empty;
    }
    public bool IsEquipped => !string.IsNullOrEmpty(ParentComponentID);
}

[Serializable]
public class SavedUnitProfile
{
    public string UnitID;
    public string UnitName;
    public ChassisDataSO ChassisData;
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

[Serializable]
public class ComponentStack
{
    public ComponentDataSO BaseData;
    public int Level;
    public int Quantity;
    public ComponentStack(ComponentDataSO data, int level, int qty)
    {
        BaseData = data; Level = level; Quantity = qty;
    }
}

[Serializable]
public class ChassisStack
{
    public ChassisDataSO BaseData;
    public int Quantity;
    public ChassisStack(ChassisDataSO data, int qty)
    {
        BaseData = data; Quantity = qty;
    }
}

// ==========================================
// 2. 玩家资产总管
// ==========================================
public class PlayerInventoryManager : MonoBehaviour
{
    public static PlayerInventoryManager Instance;
    public event Action OnInventoryChanged;

    [Header("=== 核心资产 ===")]
    public int MaxUnitSlots = 8;
    public SavedUnitProfile[] HangarUnits;

    [Header("=== 实物仓库 (堆叠字典) ===")]
    private Dictionary<string, ComponentStack> componentWarehouse = new Dictionary<string, ComponentStack>();
    private Dictionary<string, ChassisStack> chassisWarehouse = new Dictionary<string, ChassisStack>();

    [Header("=== 临时实例缓存 (车间解算用) ===")]
    public List<InstancedComponent> ComponentInventory = new List<InstancedComponent>();
    public List<InstancedChassis> ChassisInventory = new List<InstancedChassis>();

    [Header("=== 游戏全局图纸库 ===")]
    public List<ChassisDataSO> AllChassisDatabase = new List<ChassisDataSO>();
    public List<ComponentDataSO> AllComponentDatabase = new List<ComponentDataSO>();

    [Header("=== 测试作弊专用 ===")]
    public List<ChassisDataSO> DebugChassisBundle = new List<ChassisDataSO>();
    public List<ComponentDataSO> DebugComponentBundle = new List<ComponentDataSO>();
    public List<AccessoryDataSO> DebugAccessoryBundle = new List<AccessoryDataSO>();

    [Header("=== 芯片仓库 ===")]
    public List<InstancedAccessory> AccessoryInventory = new List<InstancedAccessory>();

    public List<string> DefaultNamePool = new List<string> { "苍穹破裂者", "铁肺", "苦难摇篮", "西西弗斯", "哈基米", "高达" };
    private List<string> runtimeAvailableNames;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        HangarUnits = new SavedUnitProfile[MaxUnitSlots];
        InitNamePool();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.T))
        {
            foreach (var so in DebugChassisBundle) if (so != null) AddChassisToWarehouse(so, 1);
            Debug.Log("<color=cyan>【Debug】</color> 底盘已入库并堆叠。");
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            foreach (var so in DebugComponentBundle) if (so != null) AddComponentToWarehouse(so, 1, 1);
            Debug.Log("<color=orange>【Debug】</color> 零件已入库并堆叠。");
        }
#endif
    }

    // ==========================================
    // 🚀 核心：入库与出库 (堆叠逻辑)
    // ==========================================

    public void AddComponentToWarehouse(ComponentDataSO so, int level = 1, int qty = 1)
    {
        if (so == null) return;
        string key = $"{so.ComponentBaseID}_{level}";
        if (componentWarehouse.ContainsKey(key)) componentWarehouse[key].Quantity += qty;
        else componentWarehouse[key] = new ComponentStack(so, level, qty);
        OnInventoryChanged?.Invoke();
    }

    public void AddChassisToWarehouse(ChassisDataSO so, int qty = 1)
    {
        if (so == null) return;
        if (chassisWarehouse.ContainsKey(so.ChassisID)) chassisWarehouse[so.ChassisID].Quantity += qty;
        else chassisWarehouse[so.ChassisID] = new ChassisStack(so, qty);
        OnInventoryChanged?.Invoke();
    }
    public bool TryConsumeChassisFromWarehouse(ChassisDataSO so)
    {
        if (so == null) return false;

        if (chassisWarehouse.ContainsKey(so.ChassisID) && chassisWarehouse[so.ChassisID].Quantity > 0)
        {
            chassisWarehouse[so.ChassisID].Quantity--;
            OnInventoryChanged?.Invoke();
            Debug.Log($"<color=red>【仓库】</color> 出库底盘: {so.ChassisName}，剩余: {chassisWarehouse[so.ChassisID].Quantity}");
            return true;
        }

        Debug.LogWarning($"【仓库】底盘 {so.ChassisName} 库存不足！");
        return false;
    }
    public bool TryConsumeFromWarehouse(ComponentDataSO so, int level)
    {
        string key = $"{so.ComponentBaseID}_{level}";
        if (componentWarehouse.ContainsKey(key) && componentWarehouse[key].Quantity > 0)
        {
            componentWarehouse[key].Quantity--;
            OnInventoryChanged?.Invoke();
            return true;
        }
        return false;
    }

    public List<ComponentStack> GetAvailableStacks() => componentWarehouse.Values.Where(s => s.Quantity > 0).OrderByDescending(s => s.Level).ToList();
    public List<ChassisStack> GetChassisStacks() => chassisWarehouse.Values.Where(s => s.Quantity > 0).ToList();

    // ==========================================
    // 🛠️ 辅助工具与兼容逻辑
    // ==========================================

    public string GetNextAvailableName()
    {
        if (runtimeAvailableNames == null || runtimeAvailableNames.Count == 0) InitNamePool();
        int idx = UnityEngine.Random.Range(0, runtimeAvailableNames.Count);
        string n = runtimeAvailableNames[idx];
        runtimeAvailableNames.RemoveAt(idx);
        return n;
    }

    public void ReturnNameToPool(string oldName)
    {
        if (DefaultNamePool.Contains(oldName) && !runtimeAvailableNames.Contains(oldName)) runtimeAvailableNames.Add(oldName);
    }

    private void InitNamePool() => runtimeAvailableNames = new List<string>(DefaultNamePool);

    public static float GetStatValue(List<StatEntry> stats, StatType targetStat)
    {
        if (stats == null) return 0f;
        foreach (var stat in stats) if (stat.StatID == targetStat) return stat.Value;
        return 0f;
    }

    public InstancedAccessory GetAccessoryInstance(string id) => AccessoryInventory.Find(a => a.InstanceID == id);
    public void AddAccessoryToInventory(AccessoryDataSO so) { AccessoryInventory.Add(new InstancedAccessory(so)); OnInventoryChanged?.Invoke(); }
    public void ForceTriggerInventoryEvent() => OnInventoryChanged?.Invoke();

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
        return currentHP > 0;
    }
}