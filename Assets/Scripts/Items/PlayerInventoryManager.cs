using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ==========================================
// 1. 实物档案与仓库视图
// ==========================================
[Serializable]
public class InstancedChassis
{
    public string CustomName;
    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? BaseData?.ChassisName : CustomName;
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
    public string CustomName;
    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? BaseData?.ComponentName : CustomName;
    public string InstanceID;
    public ComponentDataSO BaseData;
    public string EquippedUnitID;
    public int CurrentMark = 1;
    public List<string> SocketedAccessoryIDs = new List<string>();
    public ComponentQuality Quality = ComponentQuality.Standard;
    public float QualityScore;
    public List<StatEntry> RolledStats = new List<StatEntry>();
    public List<ComponentAffixInstance> Affixes = new List<ComponentAffixInstance>();
    public bool IsLocked;
    public int CraftSeed;
    public float Craftsmanship;
    public string CraftedAtBuildingID;
    public List<string> CraftedByResidentIDs = new List<string>();

    public InstancedComponent(ComponentDataSO data, int level)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        CurrentMark = level;
        EquippedUnitID = string.Empty;
        SocketedAccessoryIDs = new List<string>();
        RolledStats = new List<StatEntry>();
        Affixes = new List<ComponentAffixInstance>();
        CraftedAtBuildingID = string.Empty;
        CraftedByResidentIDs = new List<string>();
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
    public ComponentQuality Quality;
    public List<InstancedComponent> Instances = new List<InstancedComponent>();
    public int Quantity => Instances != null ? Instances.Count : 0;
    public InstancedComponent Representative => Quantity > 0 ? Instances[0] : null;

    public ComponentStack(ComponentDataSO data, int level, ComponentQuality quality,
        IEnumerable<InstancedComponent> instances)
    {
        BaseData = data;
        Level = level;
        Quality = quality;
        Instances = instances != null ? new List<InstancedComponent>(instances) : new List<InstancedComponent>();
    }
}

[Serializable]
public class ChassisStack
{
    public InstancedChassis Instance;
    public ChassisStack(InstancedChassis item) { Instance = item; BaseData = item.BaseData; Quantity = 1; }
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

    [Header("=== 实物仓库 ===")]
    private Dictionary<string, ChassisStack> chassisWarehouse = new Dictionary<string, ChassisStack>();

    [Header("=== 永久实例仓库 ===")]
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

        NormalizeLegacyComponents();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (UIInputFocus.IsEditingText) return;
        if (Input.GetKeyDown(KeyCode.T))
        {
            foreach (var so in DebugChassisBundle) if (so != null) AddChassisToWarehouse(so, 1);
            Debug.Log("<color=cyan>【Debug】</color> 底盘已按独立实例入库。");
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            foreach (var so in DebugComponentBundle) if (so != null) AddComponentToWarehouse(so, 1, 1);
            Debug.Log("<color=orange>【Debug】</color> 已生成带独立品质的测试组件。");
        }
#endif
    }

    // ==========================================
    // 🚀 核心：组件保持永久实例；堆叠只在查询时生成展示视图
    // ==========================================

    public void AddComponentToWarehouse(ComponentDataSO so, int level = 1, int qty = 1)
    {
        if (so == null || qty <= 0) return;
        for (int i = 0; i < qty; i++)
        {
            InstancedComponent component = new InstancedComponent(so, level);
            ComponentQualityGenerator.EnsureGenerated(component);
            ComponentInventory.Add(component);
            if (LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
                LogisticsManager.Instance.Deposit("component:" + component.InstanceID, 1);
        }
        OnInventoryChanged?.Invoke();
    }

    public void AddComponentInstance(InstancedComponent component, bool deposit = true)
    {
        if (component?.BaseData == null) return;
        if (string.IsNullOrWhiteSpace(component.InstanceID)) component.InstanceID = Guid.NewGuid().ToString();
        if (ComponentInventory.Any(item => item != null && item.InstanceID == component.InstanceID)) return;
        ComponentQualityGenerator.EnsureGenerated(component);
        component.EquippedUnitID = string.Empty;
        ComponentInventory.Add(component);
        if (deposit && LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
            LogisticsManager.Instance.Deposit("component:" + component.InstanceID, 1);
        OnInventoryChanged?.Invoke();
    }

    public bool TryEquipComponent(InstancedComponent component, string unitID)
    {
        if (component == null || string.IsNullOrWhiteSpace(unitID) || component.IsEquipped) return false;
        if (LogisticsManager.Instance != null && LogisticsManager.Instance.Ready &&
            !LogisticsManager.Instance.ConsumeItem("component:" + component.InstanceID)) return false;
        if (!ComponentInventory.Contains(component)) ComponentInventory.Add(component);
        component.EquippedUnitID = unitID;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void ReleaseComponent(InstancedComponent component)
    {
        if (component == null) return;
        bool wasEquipped = component.IsEquipped;
        component.EquippedUnitID = string.Empty;
        if (wasEquipped && LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
            LogisticsManager.Instance.Deposit("component:" + component.InstanceID, 1);
        OnInventoryChanged?.Invoke();
    }

    public InstancedComponent GetComponentInstance(string instanceID) =>
        ComponentInventory.Find(item => item != null && item.InstanceID == instanceID);

    public InstancedChassis GetChassisInstance(string id) => ChassisInventory.Find(x => x != null && x.InstanceID == id);
    public static string ChassisKey(InstancedChassis item) => "chassis-instance:" + item.InstanceID;
    public InstancedChassis CreateChassis(ChassisDataSO definition, bool deposit = true)
    {
        var item = new InstancedChassis(definition);
        ChassisInventory.Add(item);
        if (deposit && LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
            LogisticsManager.Instance.Deposit(ChassisKey(item), 1);
        OnInventoryChanged?.Invoke();
        return item;
    }
    public void AddChassisToWarehouse(ChassisDataSO so, int qty = 1)
    {
        if (so == null) return;
        for (int i = 0; i < qty; i++) CreateChassis(so);
    }
    public bool TryTakeChassis(InstancedChassis item, string unitID)
    {
        if (item == null || string.IsNullOrWhiteSpace(unitID) || item.IsEquipped || !ChassisInventory.Contains(item)) return false;
        var manager = LogisticsManager.Instance;
        if (manager != null && manager.Ready && !manager.ConsumeItem(ChassisKey(item))) return false;
        item.EquippedUnitID = unitID;
        OnInventoryChanged?.Invoke();
        return true;
    }
    public bool TryConsumeChassisFromWarehouse(ChassisDataSO so)
    {
        return TryTakeChassis(GetAvailableChassis().FirstOrDefault(x => x.BaseData == so), "reserved");
    }
    public void ReleaseChassis(SavedUnitProfile profile)
    {
        var item = GetChassisInstance(profile.ChassisInstanceID);
        if (item == null)
        {
            item = new InstancedChassis(profile.ChassisData) { InstanceID = profile.ChassisInstanceID, EquippedUnitID = profile.UnitID };
            if (string.IsNullOrEmpty(item.InstanceID)) item.InstanceID = Guid.NewGuid().ToString();
            ChassisInventory.Add(item);
        }
        if (!item.IsEquipped) return;
        item.EquippedUnitID = string.Empty;
        if (LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
            LogisticsManager.Instance.Deposit(ChassisKey(item), 1);
        OnInventoryChanged?.Invoke();
    }
    public List<InstancedChassis> GetAvailableChassis(bool includeInTransit = false)
    {
        return ChassisInventory.Where(x => x?.BaseData != null && !x.IsEquipped)
            .Where(x => includeInTransit || LogisticsManager.Instance == null || !LogisticsManager.Instance.Ready ||
                LogisticsManager.Instance.ItemAvailable(ChassisKey(x))).ToList();
    }
    // Convert the legacy aggregate inventory only when no physical ledger exists.
    public void MigrateLegacyChassisInventory()
    {
        foreach (var stack in chassisWarehouse.Values)
            for (int i = 0; i < stack.Quantity; i++) CreateChassis(stack.BaseData, false);
        chassisWarehouse.Clear();
    }
    public ChassisDataSO ResolveChassis(string id) => AllChassisDatabase.Concat(DebugChassisBundle)
        .Concat(chassisWarehouse.Values.Select(x => x.BaseData)).FirstOrDefault(x => x != null && x.ChassisID == id);
    public void ClearLegacyChassisCounts() => chassisWarehouse.Clear();
    public bool RenameItem(string key, string name)
    {
        name = (name ?? "").Trim();
        if (name.Length > 24 || name.Any(char.IsControl) || name.Contains("<") || name.Contains(">")) return false;
        if (key.StartsWith("component:"))
        {
            var item = GetComponentInstance(key.Substring(10));
            if (item == null) return false;
            item.CustomName = name;
        }
        else if (key.StartsWith("chassis-instance:"))
        {
            var item = GetChassisInstance(key.Substring(17));
            if (item == null) return false;
            item.CustomName = name;
        }
        else return false;
        OnInventoryChanged?.Invoke();
        return true;
    }
    public List<InstancedComponent> GetAvailableComponents(bool includeInTransit = false)
    {
        NormalizeLegacyComponents();
        return ComponentInventory
            .Where(component => component != null && component.BaseData != null && !component.IsEquipped)
            .Where(component => includeInTransit || LogisticsManager.Instance == null || !LogisticsManager.Instance.Ready ||
                LogisticsManager.Instance.ItemAvailable("component:" + component.InstanceID))
            .OrderByDescending(component => component.Quality)
            .ThenByDescending(component => component.CurrentMark)
            .ThenBy(component => component.BaseData.ComponentName)
            .ToList();
    }

    // 保留旧返回类型以兼容现有 UI，但每个视图只代表一个永久组件实例。
    public List<ComponentStack> GetAvailableStacks() => GetAvailableComponents()
        .Select(component => new ComponentStack(component.BaseData, component.CurrentMark,
            component.Quality, new[] { component }))
        .OrderByDescending(stack => stack.Quality)
        .ThenByDescending(stack => stack.Level)
        .ThenBy(stack => stack.BaseData.ComponentName)
        .ToList();
    public List<ChassisStack> GetChassisStacks() => GetAvailableChassis().Select(x => new ChassisStack(x)).ToList();

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
        save.ChassisAreInstances = true;
        foreach (InstancedComponent item in ComponentInventory)
        {
            if (item?.BaseData == null) continue;
            save.Components.Add(new InstancedComponentSaveData
            {
                InstanceID = item.InstanceID,
                DefinitionID = item.BaseData.ComponentBaseID,
                EquippedUnitID = item.EquippedUnitID,
                CustomName = item.CustomName,
                CurrentMark = item.CurrentMark,
                SocketedAccessoryIDs = new List<string>(item.SocketedAccessoryIDs ?? new List<string>()),
                Quality = item.Quality,
                QualityScore = item.QualityScore,
                RolledStats = CloneStats(item.RolledStats),
                Affixes = CloneAffixes(item.Affixes),
                IsLocked = item.IsLocked,
                CraftSeed = item.CraftSeed,
                Craftsmanship = item.Craftsmanship,
                CraftedAtBuildingID = item.CraftedAtBuildingID,
                CraftedByResidentIDs = new List<string>(item.CraftedByResidentIDs ?? new List<string>())
            });
        }
        foreach (InstancedChassis item in ChassisInventory)
        {
            if (item?.BaseData == null) continue;
            save.Chassis.Add(new InstancedChassisSaveData
            {
                InstanceID = item.InstanceID,
                DefinitionID = item.BaseData.ChassisID,
                EquippedUnitID = item.EquippedUnitID,
                CustomName = item.CustomName
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
            if (definition == null || item.Quantity <= 0) continue;
            for (int i = 0; i < item.Quantity; i++)
                ComponentInventory.Add(new InstancedComponent(definition, item.Level));
        }
        foreach (ChassisStackSaveData item in save.ChassisAreInstances ? new List<ChassisStackSaveData>() : save.ChassisWarehouse)
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
                CustomName = item.CustomName,
                SocketedAccessoryIDs = new List<string>(item.SocketedAccessoryIDs ?? new List<string>()),
                Quality = item.Quality,
                QualityScore = item.QualityScore,
                RolledStats = CloneStats(item.RolledStats),
                Affixes = CloneAffixes(item.Affixes),
                IsLocked = item.IsLocked,
                CraftSeed = item.CraftSeed,
                Craftsmanship = item.Craftsmanship,
                CraftedAtBuildingID = item.CraftedAtBuildingID ?? string.Empty,
                CraftedByResidentIDs = new List<string>(item.CraftedByResidentIDs ?? new List<string>())
            };
            ComponentQualityGenerator.EnsureGenerated(restored);
            if (!ComponentInventory.Any(existing => existing.InstanceID == restored.InstanceID))
                ComponentInventory.Add(restored);
        }
        foreach (InstancedChassisSaveData item in save.Chassis)
        {
            ChassisDataSO definition = definitions.ResolveChassis(item.DefinitionID);
            if (definition == null) continue;
            InstancedChassis restored = new InstancedChassis(definition)
            {
                InstanceID = item.InstanceID,
                EquippedUnitID = item.EquippedUnitID,
                CustomName = item.CustomName
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
        NormalizeLegacyComponents();
        OnInventoryChanged?.Invoke();
    }

    public bool ValidateHPBeforeUnequip(SavedUnitProfile unit, InstancedComponent componentToRemove, InstancedComponent componentToEquip = null)
    {
        float currentHP = unit.CurrentHP;
        if (componentToRemove != null)
        {
            currentHP -= ComponentStatResolver.GetValue(componentToRemove, StatType.AddedHP);
        }
        if (componentToEquip != null)
        {
            currentHP += ComponentStatResolver.GetValue(componentToEquip, StatType.AddedHP);
        }
        return currentHP > 0;
    }

    private void NormalizeLegacyComponents()
    {
        ComponentInventory = ComponentInventory ?? new List<InstancedComponent>();
        foreach (InstancedComponent component in ComponentInventory)
            ComponentQualityGenerator.EnsureGenerated(component);
    }

    private static List<StatEntry> CloneStats(IEnumerable<StatEntry> source)
    {
        if (source == null) return new List<StatEntry>();
        return source.Where(item => item != null).Select(item => new StatEntry
        {
            StatID = item.StatID,
            Value = item.Value,
            ModType = item.ModType
        }).ToList();
    }

    private static List<ComponentAffixInstance> CloneAffixes(IEnumerable<ComponentAffixInstance> source)
    {
        if (source == null) return new List<ComponentAffixInstance>();
        return source.Where(item => item != null).Select(item => new ComponentAffixInstance
        {
            AffixID = item.AffixID,
            DisplayName = item.DisplayName,
            Description = item.Description,
            Modifiers = CloneStats(item.Modifiers)
        }).ToList();
    }
}
