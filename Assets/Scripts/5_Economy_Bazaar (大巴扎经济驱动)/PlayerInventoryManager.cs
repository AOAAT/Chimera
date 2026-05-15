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
// --- PlayerInventoryManager.cs ---
public class InstancedComponent
{
    public string InstanceID;
    public ComponentDataSO BaseData;
    public string EquippedUnitID;
    public int CurrentLevel = 1;

    // 👇【核心新增】：在这里添加插槽列表
    public List<string> SocketedAccessoryIDs = new List<string>();

    public InstancedComponent(ComponentDataSO data, int level)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        CurrentLevel = level;
        EquippedUnitID = string.Empty;

        // 👇【核心新增】：确保初始化
        SocketedAccessoryIDs = new List<string>();
    }
    public int GetMaxSockets()
    {
        if (BaseData == null) return 0;

        // 🌟 关键：去等级矩阵里找当前等级的数据
        var lvData = BaseData.GetLevelData(this.CurrentLevel);

        // 如果找到了就用配置的，找不到（比如配置丢了）就给 1 个保底
        return lvData != null ? lvData.MaxSocketCount : 1;
    }

    // 快捷判断：是否还能塞芯片？
    public bool HasEmptySocket() => SocketedAccessoryIDs.Count < GetMaxSockets();
    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}



[Serializable]
public class InstancedAccessory
{
    public string InstanceID;
    public AccessoryDataSO BaseData;

    // 当前插在哪个零件上？(InstanceID)
    public string ParentComponentID = string.Empty;

    public InstancedAccessory(AccessoryDataSO data)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        ParentComponentID = string.Empty;
    }

    public bool IsEquipped => !string.IsNullOrEmpty(ParentComponentID);
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
    public List<ChassisDataSO> DebugChassisBundle = new List<ChassisDataSO>(); // 改为 List
    public List<ComponentDataSO> DebugComponentBundle = new List<ComponentDataSO>();
    public List<AccessoryDataSO> DebugAccessoryBundle = new List<AccessoryDataSO>();
    [Header("=== 芯片仓库 ===")]
    public List<InstancedAccessory> AccessoryInventory = new List<InstancedAccessory>();

    // 获取所有闲置芯片（用于 UI 列表展示）
    public List<InstancedAccessory> GetIdleAccessories()
    {
        return AccessoryInventory.FindAll(a => !a.IsEquipped);
    }

    // 通过 ID 寻找芯片实例
    public InstancedAccessory GetAccessoryInstance(string id)
    {
        return AccessoryInventory.Find(a => a.InstanceID == id);
    }

    // 获得新芯片入库
    public void AddAccessoryToInventory(AccessoryDataSO so)
    {
        AccessoryInventory.Add(new InstancedAccessory(so));
        OnInventoryChanged?.Invoke(); // 触发 UI 刷新
    }

    [Tooltip("预设的机甲名称库，玩家新建机甲时会从中随机抽取")]
    public List<string> DefaultNamePool = new List<string> {
    "苍穹破裂者", "铁肺", "苦难摇篮", "西西弗斯", "黑匣子",
    "零号病人", "柴油之心", "锈蚀审判", "无声呐喊", "利维坦",
    "赤红风暴","小小","哈基米","故障机器人","危险流浪者","高达","RaChatZ"
};

    // 运行时剩余可用的名称
    private List<string> runtimeAvailableNames;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("<color=green>【系统】</color> 资产总管已上线。");
        }
        else
        {
            Debug.LogError($"<color=red>【致命警告】</color> 场景中存在重复的 PlayerInventoryManager！物体名为：{gameObject.name}。系统已将其自动销毁。");
            Destroy(this.gameObject);
            return;
        }

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
#if UNITY_EDITOR
        // 只有在编辑器模式下，这些按键才会编译进程序
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (DebugChassisBundle != null && DebugChassisBundle.Count > 0)
            {
                foreach (var chassisSO in DebugChassisBundle)
                {
                    if (chassisSO != null) AddChassisToInventory(chassisSO);
                }
                Debug.Log("<color=cyan>【Debug】</color> 编辑器指令：底盘包已注入。");
            }
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (DebugComponentBundle != null && DebugComponentBundle.Count > 0)
            {
                foreach (var blueprint in DebugComponentBundle)
                {
                    if (blueprint != null) AddComponentToInventory(blueprint);
                }
                Debug.Log("<color=orange>【Debug】</color> 编辑器指令：零件包已注入。");
            }
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (DebugAccessoryBundle != null && DebugAccessoryBundle.Count > 0)
            {
                foreach (var chipSO in DebugAccessoryBundle)
                {
                    // 👇【核心加固】：只有图纸不为空，且 ID 不为空时才准入库
                    if (chipSO != null && !string.IsNullOrEmpty(chipSO.AccessoryID))
                    {
                        AddAccessoryToInventory(chipSO);
                    }
                }
                Debug.Log("<color=#FF00FF>【Debug】</color> 逻辑芯片包已注入。");
            }
        }

#endif
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
        // --- 👇【核心新增】：配件无损回收协议 ---
        // a 是主目标（保留配件），b 是祭品（配件退回仓库）
        if (b.SocketedAccessoryIDs != null && b.SocketedAccessoryIDs.Count > 0)
        {
            foreach (string accID in b.SocketedAccessoryIDs)
            {
                var acc = GetAccessoryInstance(accID);
                if (acc != null) acc.ParentComponentID = string.Empty; // 解绑，回归自由身
            }
            Debug.Log($"<color=yellow>【回收】</color> 祭品 [{b.BaseData.ComponentName}] 的配件已安全退回仓库。");
        }
        // ------------------------------------

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

        foreach (string compID in unit.EquippedComponentIDs)
        {
            var comp = ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null)
            {
                comp.EquippedUnitID = string.Empty;
                // 注意：这里不需要释放芯片，因为芯片是插在“零件”里的，而不是插在“机甲”里的。
                // 只要零件回库，芯片就跟着零件走。
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

    public void DismantleComponent(InstancedComponent target)
    {
        if (target == null || target.IsEquipped) return;

        // --- 👇【核心新增】：拆解零件时，强制弹出所有配件 ---
        if (target.SocketedAccessoryIDs != null && target.SocketedAccessoryIDs.Count > 0)
        {
            foreach (string accID in target.SocketedAccessoryIDs)
            {
                var acc = GetAccessoryInstance(accID);
                if (acc != null) acc.ParentComponentID = string.Empty;
            }
        }
        // -------------------------------------------

        ComponentInventory.Remove(target);
        ForceTriggerInventoryEvent();
    }


    public void ForceTriggerInventoryEvent()
    {
        OnInventoryChanged?.Invoke();
    }

    public void ExecuteDismantleAccessory(InstancedAccessory acc)
    {
        // 安全拦截：如果是空的，或者正插在武器上，禁止拆解
        if (acc == null || acc.IsEquipped)
        {
            Debug.LogWarning("【系统拦截】尝试拆解不存在或正在使用的芯片。");
            return;
        }

        // 1. 经济结算：读取该芯片图纸里配好的 ScrapValue
        if (GlobalResourceManager.Instance != null)
        {
            GlobalResourceManager.Instance.ModifyMaterials(acc.BaseData.ScrapValue);
        }

        // 2. 物理注销：从列表里彻底踢出
        if (AccessoryInventory.Contains(acc))
        {
            AccessoryInventory.Remove(acc);
        }

        Debug.Log($"<color=red>【逻辑粉碎】</color> 成功熔毁了芯片 [{acc.BaseData.AccessoryName}]");

        // 3. 广播刷新：让仓库 UI 发现少了一个东西，自动重刷
        ForceTriggerInventoryEvent();
    }
    public bool EquipAccessoryToComponent(string accessoryInstanceID, InstancedComponent targetComp, out string error)
    {
        var chip = GetAccessoryInstance(accessoryInstanceID);
        if (chip == null || chip.IsEquipped)
        {
            error = "芯片不存在或已被占用";
            return false;
        }

        if (AccessoryValidator.CanFitAccessory(targetComp, chip.BaseData, out error))
        {
            // 1. 建立双向绑定
            targetComp.SocketedAccessoryIDs.Add(accessoryInstanceID);
            chip.ParentComponentID = targetComp.InstanceID;

            Debug.Log($"<color=#00FF00>【芯片注入】</color> [{chip.BaseData.AccessoryName}] 已成功装入 [{targetComp.BaseData.ComponentName}]");

            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void UnequipAccessoryFromComponent(string accessoryInstanceID, InstancedComponent targetComp)
    {
        var chip = GetAccessoryInstance(accessoryInstanceID);
        if (chip != null && targetComp.SocketedAccessoryIDs.Contains(accessoryInstanceID))
        {
            targetComp.SocketedAccessoryIDs.Remove(accessoryInstanceID);
            chip.ParentComponentID = string.Empty;

            Debug.Log($"<color=yellow>【芯片剥离】</color> 已从零件中回收芯片。");
            OnInventoryChanged?.Invoke();
        }
    }

    public void ValidateAccessoryCapacity(InstancedComponent comp)
    {
        int max = comp.GetMaxSockets();
        while (comp.SocketedAccessoryIDs.Count > max)
        {
            // 强制弹出最后一个配件
            string lastAccID = comp.SocketedAccessoryIDs[comp.SocketedAccessoryIDs.Count - 1];
            UnequipAccessoryFromComponent(lastAccID, comp);
            Debug.LogWarning($"<color=orange>【硬件降级】</color> 零件插槽收缩，配件已自动弹出。");
        }
    }
}