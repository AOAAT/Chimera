// --- START OF FILE RuntimeChimeraData.cs ---
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RuntimeWeapon
{
    public string WeaponName;
    public ComponentDataSO SourceSO;
    public Dictionary<StatType, float> WeaponStats = new Dictionary<StatType, float>();
    public List<ECABlock> WeaponMechanics = new List<ECABlock>();

    public WeaponDeliveryType DeliveryType;
    public GameObject ProjectilePrefab;

    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    public float BonusCriticalChance = 0f;

    // 👇【新增】：万能状态机字典，用来存弹夹余量、开火次数等！
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }
}

[System.Serializable]
public class RuntimeChimeraData
{
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

    // 存储本台机甲的主动技能
    public ActiveSkillConfig CoreActiveSkill;

    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components)
    {
        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        Tags.Clear();
        GlobalMechanics.Clear();
        EquippedWeapons.Clear();
        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;
        CoreActiveSkill = null; // 每次重装前清空

        if (chassis == null) return;
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;

        // 1. 读取底盘属性
        ProcessStats(chassis.BaseStats, false, null);

        // 2. 遍历所有组件
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var compSO = compInstance.BaseData;
            AllEquippedSOs.Add(compSO);

            var levelData = compSO.GetLevelData(compInstance.CurrentLevel);
            if (levelData == null) continue;

            foreach (var tag in compSO.BaseSubTags) Tags.Add(tag);
            foreach (var tag in levelData.BonusTags) Tags.Add(tag);

            if (compSO.Type == ComponentType.Weapon)
            {
                RuntimeWeapon newWeapon = new RuntimeWeapon { WeaponName = compSO.ComponentName };
                newWeapon.SourceSO = compSO;
                newWeapon.DeliveryType = compSO.DeliveryType;
                newWeapon.ProjectilePrefab = compSO.ProjectilePrefab;

                if (levelData.OnHitActions != null) newWeapon.OnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnFireActions != null) newWeapon.OnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.Mechanics != null) newWeapon.WeaponMechanics.AddRange(levelData.Mechanics);

                ProcessStats(levelData.Stats, true, newWeapon);
                EquippedWeapons.Add(newWeapon);
            }
            else
            {
                if (compSO.Type == ComponentType.Core)
                {
                    TargetingLogic = compSO.TargetingLogic;
                    MovementLogic = compSO.MovementLogic;
                    SafeDodgeDistance = compSO.SafeDodgeDistance;

                    // 提取核心主动技能
                    if (levelData.ActiveSkill == null)
                    {
                        Debug.LogWarning($"<color=#FFA500>[Assemble]</color> 核心 [{compSO.ComponentName}] 的 ActiveSkill 为空！");
                    }
                    else if (!levelData.ActiveSkill.HasActiveSkill)
                    {
                        Debug.LogWarning($"<color=#FFA500>[Assemble]</color> 核心 [{compSO.ComponentName}] (Lv.{compInstance.CurrentLevel}) 没有勾选 HasActiveSkill！");
                    }
                    else
                    {
                        CoreActiveSkill = levelData.ActiveSkill;
                        Debug.Log($"<color=#00FFFF>[Assemble]</color> 成功提取主动技能: [{CoreActiveSkill.SkillName}]");
                    }
                }

                if (levelData.Mechanics != null) GlobalMechanics.AddRange(levelData.Mechanics);
                ProcessStats(levelData.Stats, false, null);
            }
        }

        // 3. 统计最终面板
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);

        // 4. 执行装配 ECA 机制
        foreach (var compInstance in components)
        {
            if (compInstance == null) continue;
            var levelData = compInstance.BaseData.GetLevelData(compInstance.CurrentLevel);
            if (levelData != null && levelData.OnAssembleActions.Count > 0)
            {
                ECAContext assembleContext = new ECAContext { ChassisData = this, SourceComponentSO = compInstance.BaseData };
                foreach (var action in levelData.OnAssembleActions) if (action != null) action.Execute(assembleContext);
            }
        }
    }

    // ==========================================
    // 极其关键的属性读取算法 (绝对不能省略！)
    // ==========================================
    private void ProcessStats(List<StatEntry> stats, bool isWeaponLocal, RuntimeWeapon weaponRef)
    {
        if (stats == null) return;

        foreach (var stat in stats)
        {
            if (isWeaponLocal && IsWeaponSpecificStat(stat.StatID))
            {
                if (weaponRef.WeaponStats.ContainsKey(stat.StatID))
                    weaponRef.WeaponStats[stat.StatID] += stat.Value;
                else
                    weaponRef.WeaponStats.Add(stat.StatID, stat.Value);
            }
            else
            {
                if (GlobalStats.ContainsKey(stat.StatID))
                    GlobalStats[stat.StatID] += stat.Value;
                else
                    GlobalStats.Add(stat.StatID, stat.Value);
            }
        }
    }

    public void ModifyStat(ComponentDataSO targetSO, StatType stat, float delta)
    {
        if (IsWeaponSpecificStat(stat))
        {
            foreach (var weapon in EquippedWeapons)
            {
                if (weapon.SourceSO == targetSO)
                {
                    if (weapon.WeaponStats.ContainsKey(stat)) weapon.WeaponStats[stat] += delta;
                    else weapon.WeaponStats[stat] = delta;
                    return;
                }
            }
        }

        if (stat == StatType.PowerCost) TotalPowerCost += delta;
        else if (stat == StatType.AddedHP) MaxHP += delta;
        else if (stat == StatType.AddedAP) MaxAP += delta;
        else if (stat == StatType.AddedMass) TotalMass += delta;
        else if (stat == StatType.EnginePower) TotalEnginePower += delta;

        if (GlobalStats.ContainsKey(stat)) GlobalStats[stat] += delta;
        else GlobalStats.Add(stat, delta);
    }

    private bool IsWeaponSpecificStat(StatType type)
    {
        return type == StatType.MaxDamage ||
               type == StatType.MinDamage ||
               type == StatType.MaxRange ||
               type == StatType.MinRange ||
               type == StatType.AttackSpeed ||
               type == StatType.CriticalChance ||
               type == StatType.ExplosionRadius ||
               type == StatType.MultiShotCount ||
               type == StatType.ProjectileSpeed;
    }

    public float GetGlobalStat(StatType statID)
    {
        return GlobalStats.ContainsKey(statID) ? GlobalStats[statID] : 0f;
    }
}