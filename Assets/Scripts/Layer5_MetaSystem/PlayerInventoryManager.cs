using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ==========================================
// 1. 底盘实体的“身份证” (消耗品概念)
// ==========================================
[Serializable]
public class InstancedChassis
{
    public string InstanceID; // 宇宙唯一的身份证号
    public ChassisDataSO BaseData; // 指向底盘图纸
    public string EquippedUnitID; // 记录它被组装成了哪台机甲。如果为空，说明在仓库里吃灰。

    public InstancedChassis(ChassisDataSO data)
    {
        InstanceID = Guid.NewGuid().ToString();
        BaseData = data;
        EquippedUnitID = string.Empty;
    }

    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}

// ==========================================
// 2. 散件实体的“身份证” (独立占格)
// ==========================================
[Serializable]
public class InstancedComponent
{
    public string InstanceID; // 宇宙唯一的身份证号
    public ComponentDataSO BaseData; // 指向图纸，获取图标、属性、Tag等
    public string EquippedUnitID; // 【核心设计】记录它装在哪个机甲上。为空代表在仓库里闲置。

    // 构造函数：每当玩家获得一个新零件，就给它发一个新身份证
    public InstancedComponent(ComponentDataSO data)
    {
        InstanceID = Guid.NewGuid().ToString(); // 生成类似 "b4a5d... " 的唯一码
        BaseData = data;
        EquippedUnitID = string.Empty;
    }

    // 方便UI判断是否被占用
    public bool IsEquipped => !string.IsNullOrEmpty(EquippedUnitID);
}

// ==========================================
// 3. 机甲的持久化档案 (占用8个车库槽位)
// ==========================================
[Serializable]
public class SavedUnitProfile
{
    public string UnitID;
    public string UnitName;
    public ChassisDataSO ChassisData; // 底盘图纸

    [Header("战损记录")]
    public float CurrentHP;
    public float CurrentAP; // 虽然战斗后会回满，但存下来以备不时之需

    public bool IsDeployed = false;

    // 记录插槽装配关系：Key是底盘的插槽索引(0, 1, 2...)，Value是 InstancedComponent 的身份证号
    public List<int> SlotIndices = new List<int>();
    public List<string> EquippedComponentIDs = new List<string>();

    public string ChassisInstanceID; // 【新增】记录消耗的具体是哪个底盘实体

    // 构造函数改成接收“底盘实体”，而不是“底盘图纸”
    public SavedUnitProfile(InstancedChassis chassisInstance, string name = "未命名机甲")
    {
        UnitID = Guid.NewGuid().ToString();
        UnitName = name;

        ChassisData = chassisInstance.BaseData;
        ChassisInstanceID = chassisInstance.InstanceID; // 锁定消耗的具体实体！

        // 统一使用查字典的方式读取底盘的初始贡献血量与护甲
        CurrentHP = PlayerInventoryManager.GetStatValue(ChassisData.BaseStats, StatType.AddedHP);
        CurrentAP = PlayerInventoryManager.GetStatValue(ChassisData.BaseStats, StatType.AddedAP);
    }
}

// ==========================================
// 4. 玩家资产总管 (挂载到场景里的空物体上)
// ==========================================
public class PlayerInventoryManager : MonoBehaviour
{
    public static PlayerInventoryManager Instance;

    public event Action OnInventoryChanged; // 【新增】全局大喇叭！

    [Header("=== 核心资产 ===")]
    public int MaxUnitSlots = 8; // 硬核机库上限
    public SavedUnitProfile[] HangarUnits;

    [Header("=== 实体仓库 ===")]
    public List<InstancedChassis> ChassisInventory = new List<InstancedChassis>(); // 【这就是为你补上的底盘仓库】
    public List<InstancedComponent> ComponentInventory = new List<InstancedComponent>(); // 玩家拥有的所有散件实体

    [Header("=== 测试作弊专用 (一键进货包) ===")]
    public ChassisDataSO DebugChassisBlueprint; // 底盘测试槽保留
    public List<ComponentDataSO> DebugComponentBundle = new List<ComponentDataSO>(); // 【全家桶套餐】改成列表！
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 👇 【新增】：开机时，直接用水泥浇筑 8 个空车位！
        HangarUnits = new SavedUnitProfile[MaxUnitSlots];
    }
    private void Update()
    {
        // 按下键盘的 T 键，模拟工厂造出一个底盘！
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (DebugChassisBlueprint != null)
            {
                AddChassisToInventory(DebugChassisBlueprint);
                Debug.Log("【作弊成功】叮！您的工厂刚刚为您生产了一台全新底盘，请查收！");
            }
            else
            {
                Debug.LogWarning("【作弊失败】您还没有配置 DebugChassisBlueprint 图纸！");
            }
        }

        // 【史诗级加强】：按下 Y 键，天降正义，一键获取列表里的所有零件！
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (DebugComponentBundle != null && DebugComponentBundle.Count > 0)
            {
                foreach (var blueprint in DebugComponentBundle)
                {
                    if (blueprint != null)
                    {
                        AddComponentToInventory(blueprint);
                    }
                }
                Debug.Log($"【空投抵达】爽！一键获取了 {DebugComponentBundle.Count} 个新零件！");
            }
            else
            {
                Debug.LogWarning("【作弊失败】您的零件包 (DebugComponentBundle) 是空的，请在 Inspector 里填入图纸！");
            }
        }
    }

    // ==========================================
    // 🔍 内部工具：从动态词缀池中提取指定属性的值
    // ==========================================
    public static float GetStatValue(List<StatEntry> stats, StatType targetStat)
    {
        if (stats == null) return 0f;
        foreach (var stat in stats)
        {
            if (stat.StatID == targetStat)
            {
                return stat.Value;
            }
        }
        return 0f; // 如果列表里没配这个属性，默认就是 0
    }

    // ==========================================
    // 🧠 核心查询：获取仓库列表 (支持类型与标签双重过滤，且占用置底)
    // ==========================================
    public List<InstancedComponent> GetFilteredInventory(ComponentType requiredType, ComponentTag? requiredTag = null)
    {
        var query = ComponentInventory.Where(c => c.BaseData.Type == requiredType);

        // 如果传入了 Tag (比如只看 Factory 类的武器)，执行二次过滤
        if (requiredTag.HasValue)
        {
            query = query.Where(c => c.BaseData.Tags.Contains(requiredTag.Value));
        }

        // 【极其优雅的排序】：未装备的放前面 (false 排在 true 前面)，然后按名字排序
        return query
            .OrderBy(c => c.IsEquipped)
            .ThenBy(c => c.BaseData.ComponentName)
            .ToList();
    }

    // ==========================================
    // 🩺 战地外科手术：极限装配拦截校验
    // ==========================================
    public bool ValidateHPBeforeUnequip(SavedUnitProfile unit, InstancedComponent componentToRemove, InstancedComponent componentToEquip = null)
    {
        float currentHP = unit.CurrentHP;

        // 1. 扣除旧组件的生命维持加成 (去字典里查 AddedHP 的值)
        if (componentToRemove != null)
        {
            currentHP -= GetStatValue(componentToRemove.BaseData.BaseStats, StatType.AddedHP);
        }

        // 2. 加上新组件的生命维持加成 (如果是纯卸下，这就是 null)
        if (componentToEquip != null)
        {
            currentHP += GetStatValue(componentToEquip.BaseData.BaseStats, StatType.AddedHP);
        }

        // 3. 最终死线判决！
        if (currentHP <= 0)
        {
            Debug.LogWarning($"【致命警告】拆卸被拦截！操作将导致机甲解体。预期剩余HP: {currentHP}");
            return false; // 拒绝放行！
        }

        return true; // 允许装配
    }

    public void AddComponentToInventory(ComponentDataSO so)
    {
        var newItem = new InstancedComponent(so);
        ComponentInventory.Add(newItem);
        Debug.Log($"【零件入库】获得了新实体: {so.ComponentName} | 序列号: {newItem.InstanceID}");

        // 👇 进货完毕，按喇叭！
        OnInventoryChanged?.Invoke();
    }

    // ==========================================
    // 🔧 辅助测试：工厂生产底盘下线 (生产底盘)
    // ==========================================
    public void AddChassisToInventory(ChassisDataSO so)
    {
        var newItem = new InstancedChassis(so);
        ChassisInventory.Add(newItem);
        Debug.Log($"【底盘入库】获得了新底盘实体: {so.ChassisName} | 序列号: {newItem.InstanceID}");

        // 👇 进货完毕，按喇叭！
        OnInventoryChanged?.Invoke();
    }
}