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
    public int CurrentMark = 1;
    public List<string> SocketedAccessoryIDs = new List<string>();

    public InstancedComponent(ComponentDataSO data, int level)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        CurrentMark = level;
        EquippedUnitID = string.Empty;
        SocketedAccessoryIDs = new List<string>();
    }
    public int GetMaxSockets()
    {
        if (BaseData == null) return 0;
        var lvData = BaseData.GetModelData(this.CurrentMark );
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

    [Header("=== 实物仓库 (堆叠字典) ===")]
    private Dictionary<string, ComponentStack> componentWarehouse = new Dictionary<string, ComponentStack>();
    private Dictionary<string, ChassisStack> chassisWarehouse = new Dictionary<string, ChassisStack>();

    [Header("=== 临时实例缓存 (车间解算用) ===")]
    public List<InstancedComponent> ComponentInventory = new List<InstancedComponent>();
    public List<InstancedChassis> ChassisInventory = new List<InstancedChassis>();

    [Header("=== 游戏全局图纸库 ===")]
    public List<ChassisDataSO> AllChassisDatabase = new List<ChassisDataSO>();
    public List<ComponentDataSO> AllComponentDatabase = new List<ComponentDataSO>();
    public List<AccessoryDataSO> AllAccessoryDatabase = new List<AccessoryDataSO>();

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


    public static float GetStatValue(List<StatEntry> stats, StatType targetStat)
    {
        if (stats == null) return 0f;
        foreach (var stat in stats) if (stat.StatID == targetStat) return stat.Value;
        return 0f;
    }

    public InstancedAccessory GetAccessoryInstance(string id) => AccessoryInventory.Find(a => a.InstanceID == id);
    public void AddAccessoryToInventory(AccessoryDataSO so) { AccessoryInventory.Add(new InstancedAccessory(so)); OnInventoryChanged?.Invoke(); }
    public void ForceTriggerInventoryEvent() => OnInventoryChanged?.Invoke();

    public InventorySaveData CaptureSaveData()
    {
        InventorySaveData save = new InventorySaveData();
        foreach (ComponentStack stack in componentWarehouse.Values)
        {
            if (stack?.BaseData == null) continue;
            save.ComponentWarehouse.Add(new ComponentStackSaveData
            {
                DefinitionID = stack.BaseData.ComponentBaseID,
                Level = stack.Level,
                Quantity = stack.Quantity
            });
        }
        foreach (ChassisStack stack in chassisWarehouse.Values)
        {
            if (stack?.BaseData == null) continue;
            save.ChassisWarehouse.Add(new ChassisStackSaveData
            {
                DefinitionID = stack.BaseData.ChassisID,
                Quantity = stack.Quantity
            });
        }
        foreach (InstancedComponent item in ComponentInventory)
        {
            if (item?.BaseData == null) continue;
            save.Components.Add(new InstancedComponentSaveData
            {
                InstanceID = item.InstanceID,
                DefinitionID = item.BaseData.ComponentBaseID,
                EquippedUnitID = item.EquippedUnitID,
                CurrentMark = item.CurrentMark,
                SocketedAccessoryIDs = new List<string>(item.SocketedAccessoryIDs ?? new List<string>())
            });
        }
        foreach (InstancedChassis item in ChassisInventory)
        {
            if (item?.BaseData == null) continue;
            save.Chassis.Add(new InstancedChassisSaveData
            {
                InstanceID = item.InstanceID,
                DefinitionID = item.BaseData.ChassisID,
                EquippedUnitID = item.EquippedUnitID
            });
        }
        foreach (InstancedAccessory item in AccessoryInventory)
        {
            if (item?.BaseData == null) continue;
            save.Accessories.Add(new InstancedAccessorySaveData
            {
                InstanceID = item.InstanceID,
                DefinitionID = item.BaseData.AccessoryID,
                ParentComponentID = item.ParentComponentID
            });
        }
        return save;
    }

    public void RestoreSaveData(InventorySaveData save, SaveDefinitionResolver definitions)
    {
        componentWarehouse.Clear();
        chassisWarehouse.Clear();
        ComponentInventory.Clear();
        ChassisInventory.Clear();
        AccessoryInventory.Clear();
        if (save == null)
        {
            OnInventoryChanged?.Invoke();
            return;
        }

        foreach (ComponentStackSaveData item in save.ComponentWarehouse)
        {
            ComponentDataSO definition = definitions.ResolveComponent(item.DefinitionID);
            if (definition != null && item.Quantity > 0)
                componentWarehouse[$"{definition.ComponentBaseID}_{item.Level}"] = new ComponentStack(definition, item.Level, item.Quantity);
        }
        foreach (ChassisStackSaveData item in save.ChassisWarehouse)
        {
            ChassisDataSO definition = definitions.ResolveChassis(item.DefinitionID);
            if (definition != null && item.Quantity > 0)
                chassisWarehouse[definition.ChassisID] = new ChassisStack(definition, item.Quantity);
        }
        foreach (InstancedComponentSaveData item in save.Components)
        {
            ComponentDataSO definition = definitions.ResolveComponent(item.DefinitionID);
            if (definition == null) continue;
            InstancedComponent restored = new InstancedComponent(definition, item.CurrentMark)
            {
                InstanceID = item.InstanceID,
                EquippedUnitID = item.EquippedUnitID,
                SocketedAccessoryIDs = new List<string>(item.SocketedAccessoryIDs ?? new List<string>())
            };
            ComponentInventory.Add(restored);
        }
        foreach (InstancedChassisSaveData item in save.Chassis)
        {
            ChassisDataSO definition = definitions.ResolveChassis(item.DefinitionID);
            if (definition == null) continue;
            InstancedChassis restored = new InstancedChassis(definition)
            {
                InstanceID = item.InstanceID,
                EquippedUnitID = item.EquippedUnitID
            };
            ChassisInventory.Add(restored);
        }
        foreach (InstancedAccessorySaveData item in save.Accessories)
        {
            AccessoryDataSO definition = definitions.ResolveAccessory(item.DefinitionID);
            if (definition == null) continue;
            InstancedAccessory restored = new InstancedAccessory(definition)
            {
                InstanceID = item.InstanceID,
                ParentComponentID = item.ParentComponentID
            };
            AccessoryInventory.Add(restored);
        }
        OnInventoryChanged?.Invoke();
    }

    public bool ValidateHPBeforeUnequip(SavedUnitProfile unit, InstancedComponent componentToRemove, InstancedComponent componentToEquip = null)
    {
        float currentHP = unit.CurrentHP;
        if (componentToRemove != null)
        {
            var lvData = componentToRemove.BaseData.GetModelData(componentToRemove.CurrentMark);
            if (lvData != null) currentHP -= GetStatValue(lvData.Stats, StatType.AddedHP);
        }
        if (componentToEquip != null)
        {
            var lvData = componentToEquip.BaseData.GetModelData(componentToEquip.CurrentMark);
            if (lvData != null) currentHP += GetStatValue(lvData.Stats, StatType.AddedHP);
        }
        return currentHP > 0;
    }
}
