using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RuntimeWeapon
{
    public string WeaponName;
    public ComponentDataSO SourceSO;
    public int CurrentLevel = 1;
    public Dictionary<StatType, float> WeaponStats = new Dictionary<StatType, float>();
    public List<ECABlock> WeaponMechanics = new List<ECABlock>();
    public WeaponDeliveryType DeliveryType;
    public GameObject ProjectilePrefab;
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
    public float BonusCriticalChance = 0f;
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }
}

[System.Serializable]
public class RuntimeChimeraData
{
    public ChassisDataSO ActiveChassisSO;
    public string UnitID;
    public string UnitName;
    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }
    public float TotalPowerCost { get; private set; }
    public float TotalMass { get; private set; }
    public float TotalEnginePower { get; private set; }
    public Vector2 LogicCenterOffset { get; private set; }

    public TargetingStrategy TargetingLogic;
    public MovementStrategy MovementLogic;
    public float SafeDodgeDistance;

    public Dictionary<StatType, float> GlobalStats = new Dictionary<StatType, float>();
    public List<SubTag> Tags = new List<SubTag>();
    public List<ECABlock> GlobalMechanics = new List<ECABlock>();
    public List<RuntimeWeapon> EquippedWeapons = new List<RuntimeWeapon>();
    public List<ComponentDataSO> AllEquippedSOs = new List<ComponentDataSO>();

    public ActiveSkillConfig CoreActiveSkill;
    private Dictionary<StatType, float> cachedFlattenedStats = new Dictionary<StatType, float>();

    public List<ECAAction> GlobalOnFireActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnHitActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnKillActions = new List<ECAAction>();

    // 👇【新增】：全机开战管线
    public List<ECAAction> GlobalOnBattleStartActions = new List<ECAAction>();
    public bool CanFireWhileManualMoving = false;
    // --- RuntimeChimeraData.cs 逻辑加固版 ---
    public Dictionary<ComponentDataSO, Dictionary<StatType, float>> ComponentLocalOffsets = new Dictionary<ComponentDataSO, Dictionary<StatType, float>>();
    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components, Transform entityTransform = null)
    {
        // --- 1. 彻底肃清旧状态 ---
        GlobalOnFireActions.Clear();
        GlobalOnHitActions.Clear();
        GlobalOnBattleStartActions.Clear();
        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        ComponentLocalOffsets.Clear(); // 清空原子修正字典
        EquippedWeapons.Clear();

        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;
        this.ActiveChassisSO = chassis;

        if (chassis == null) return;

        // --- 2. 阶段 A：基础数据登记 (注册阶段) ---
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;

        // 登记底盘基础属性
        ProcessStats(chassis.BaseStats, false, null);

        // 循环登记零件基础属性
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var compSO = compInstance.BaseData;
            AllEquippedSOs.Add(compSO); // 关键：先加入列表，供后续积木扫描

            var levelData = compSO.GetLevelData(compInstance.CurrentLevel);
            if (levelData == null) continue;

            // 标签合并
            if (compSO.BaseSubTags != null) foreach (var tag in compSO.BaseSubTags) Tags.Add(tag);
            if (levelData.BonusTags != null) foreach (var tag in levelData.BonusTags) Tags.Add(tag);

            if (compSO.Type == ComponentType.Weapon)
            {
                // 🌟 核心修复：先创建 RuntimeWeapon 对象
                RuntimeWeapon newWeapon = new RuntimeWeapon
                {
                    WeaponName = compSO.ComponentName,
                    SourceSO = compSO,
                    CurrentLevel = compInstance.CurrentLevel,
                    DeliveryType = compSO.DeliveryType,
                    ProjectilePrefab = compSO.ProjectilePrefab
                };

                if (levelData.OnHitActions != null) newWeapon.OnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnFireActions != null) newWeapon.OnFireActions.AddRange(levelData.OnFireActions);

                EquippedWeapons.Add(newWeapon);

                // 🌟 核心修复：传入刚创建好的 newWeapon 引用，防止 ProcessStats 内部报 NullRef
                ProcessStats(levelData.Stats, true, newWeapon);
            }
            else
            {
                if (compSO.Type == ComponentType.Core)
                {
                    TargetingLogic = compSO.TargetingLogic;
                    MovementLogic = compSO.MovementLogic;
                    SafeDodgeDistance = compSO.SafeDodgeDistance;
                    CoreActiveSkill = levelData.ActiveSkill;
                }
                // 非武器组件，传入 null
                ProcessStats(levelData.Stats, false, null);
            }

            // 收集非武器类组件的全局开火/命中/开战协议
            if (compSO.Type != ComponentType.Weapon)
            {
                if (levelData.OnFireActions != null) GlobalOnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.OnHitActions != null) GlobalOnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(levelData.OnBattleStartActions);
            }
        }

        // --- 3. 阶段 B：执行积木逻辑 (修正阶段) ---
        // 此时 AllEquippedSOs 已经满了，底盘积木可以正常通过 ByMacro 扫描到科技/血肉件了

        // A. 执行底盘积木
        if (chassis.OnAssembleActions != null)
        {
            ECAContext chassisCtx = new ECAContext { ChassisData = this, SourceEntity = entityTransform };
            foreach (var action in chassis.OnAssembleActions)
                if (action != null) action.Execute(chassisCtx);
        }
        if (chassis.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(chassis.OnBattleStartActions);

        // B. 执行零件积木
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;
            var levelData = compInstance.BaseData.GetLevelData(compInstance.CurrentLevel);

            if (levelData != null && levelData.OnAssembleActions != null && levelData.OnAssembleActions.Count > 0)
            {
                ECAContext compCtx = new ECAContext
                {
                    ChassisData = this,
                    SourceComponentSO = compInstance.BaseData,
                    SourceEntity = entityTransform
                };
                foreach (var action in levelData.OnAssembleActions)
                    if (action != null) action.Execute(compCtx);
            }
        }

        // --- 4. 阶段 C：最终解算汇总 ---
        RefreshFinalStats();
    }


    // 3. 属性修改入口：处理原子偏离
    public void ModifyStat(ComponentDataSO targetSO, StatType stat, float delta)
    {
        // 如果是耗电量或武器属性，计入零件私有偏移
        if (IsComponentSpecificStat(stat))
        {
            if (!ComponentLocalOffsets.ContainsKey(targetSO))
                ComponentLocalOffsets.Add(targetSO, new Dictionary<StatType, float>());

            if (ComponentLocalOffsets[targetSO].ContainsKey(stat))
                ComponentLocalOffsets[targetSO][stat] += delta;
            else
                ComponentLocalOffsets[targetSO].Add(stat, delta);
        }

        // 无论什么属性，都同步到全局池中，确保总电量/总血量实时变动
        if (GlobalStats.ContainsKey(stat)) GlobalStats[stat] += delta;
        else GlobalStats.Add(stat, delta);

        // 实时触发汇总刷新
        RefreshFinalStats();
    }

    // --- RuntimeChimeraData.cs 内部方法 ---

    private void RefreshFinalStats()
    {
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);

        // 👈 核心恢复：总耗电量直接读取汇总后的 GlobalStats
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
    }
    private bool IsComponentSpecificStat(StatType type)
    {
        return type == StatType.MaxDamage ||
               type == StatType.MinDamage ||
               type == StatType.MaxRange ||
               type == StatType.MinRange ||
               type == StatType.AttackSpeed ||
               type == StatType.CriticalChance ||
               type == StatType.CritMultiplier ||
               type == StatType.PowerCost || // 👈 【关键修改】：耗电量现在是原子的了
               type == StatType.ProjectileSpeed;
    }

    private void ProcessStats(List<StatEntry> stats, bool isWeaponLocal, RuntimeWeapon weaponRef)
    {
        if (stats == null) return;
        foreach (var stat in stats)
        {
            // 👇【核心加固】：只有当确定是武器属性，且武器引用确实存在时，才往私有池塞
            if (isWeaponLocal && weaponRef != null && IsWeaponSpecificStat(stat.StatID))
            {
                if (weaponRef.WeaponStats.ContainsKey(stat.StatID))
                    weaponRef.WeaponStats[stat.StatID] += stat.Value;
                else
                    weaponRef.WeaponStats.Add(stat.StatID, stat.Value);
            }
            else
            {
                // 其他所有情况（或者是武器的全局属性如 Mass, PowerCost），统一进入 Global 池
                if (GlobalStats.ContainsKey(stat.StatID))
                    GlobalStats[stat.StatID] += stat.Value;
                else
                    GlobalStats.Add(stat.StatID, stat.Value);
            }
        }
    }





    private bool IsWeaponSpecificStat(StatType type)
    {
        return type == StatType.MaxDamage ||
               type == StatType.MinDamage ||
               type == StatType.MaxRange ||
               type == StatType.MinRange ||
               type == StatType.AttackSpeed ||
               type == StatType.CriticalChance ||
               type == StatType.CritMultiplier || // 👈 【核心新增】：标记为武器私有属性
               type == StatType.ExplosionRadius ||
               type == StatType.MultiShotCount ||
               type == StatType.ProjectileSpeed;
    }

    public float GetGlobalStat(StatType statID) { return GlobalStats.ContainsKey(statID) ? GlobalStats[statID] : 0f; }
}