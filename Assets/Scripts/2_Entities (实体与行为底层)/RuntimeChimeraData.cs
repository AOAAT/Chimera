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

    public ActiveSkillConfig CoreActiveSkill;
    private Dictionary<StatType, float> cachedFlattenedStats = new Dictionary<StatType, float>();

    public List<ECAAction> GlobalOnFireActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnHitActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnKillActions = new List<ECAAction>();

    // 👇【新增】：全机开战管线
    public List<ECAAction> GlobalOnBattleStartActions = new List<ECAAction>();

    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components)
    {
        GlobalOnFireActions.Clear();
        GlobalOnHitActions.Clear();
        GlobalOnKillActions.Clear();
        GlobalOnBattleStartActions.Clear(); // 👈 每次重装清空

        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        Tags.Clear();
        GlobalMechanics.Clear();
        EquippedWeapons.Clear();
        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;
        CoreActiveSkill = null;

        if (chassis == null) return;
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;

        ProcessStats(chassis.BaseStats, false, null);

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
                RuntimeWeapon newWeapon = new RuntimeWeapon { WeaponName = compSO.ComponentName, SourceSO = compSO, DeliveryType = compSO.DeliveryType, ProjectilePrefab = compSO.ProjectilePrefab };
                if (levelData.OnHitActions != null) newWeapon.OnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnFireActions != null) newWeapon.OnFireActions.AddRange(levelData.OnFireActions);
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
                    CoreActiveSkill = levelData.ActiveSkill;
                }
                ProcessStats(levelData.Stats, false, null);
            }

            // 👇【核心新增】：如果是辅助/移动/核心组件，且配了全局效果，注入对应池
            if (compInstance.BaseData.Type != ComponentType.Weapon)
            {
                if (levelData.OnFireActions != null) GlobalOnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.OnHitActions != null) GlobalOnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(levelData.OnBattleStartActions);
            }
        }

        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);

        // 执行装配瞬时效果
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
        RefreshStatCache();
    }

    private void RefreshStatCache()
    {
        cachedFlattenedStats.Clear();
        foreach (StatType type in System.Enum.GetValues(typeof(StatType))) cachedFlattenedStats[type] = GetGlobalStat(type);
    }

    private void ProcessStats(List<StatEntry> stats, bool isWeaponLocal, RuntimeWeapon weaponRef)
    {
        if (stats == null) return;
        foreach (var stat in stats)
        {
            if (isWeaponLocal && IsWeaponSpecificStat(stat.StatID))
            {
                if (weaponRef.WeaponStats.ContainsKey(stat.StatID)) weaponRef.WeaponStats[stat.StatID] += stat.Value;
                else weaponRef.WeaponStats.Add(stat.StatID, stat.Value);
            }
            else
            {
                if (GlobalStats.ContainsKey(stat.StatID)) GlobalStats[stat.StatID] += stat.Value;
                else GlobalStats.Add(stat.StatID, stat.Value);
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

                    switch (stat)
                    {
                        case StatType.MinDamage: case StatType.MaxDamage: weapon.WeaponStats[stat] = Mathf.Max(0.1f, weapon.WeaponStats[stat]); break;
                        case StatType.AttackSpeed: weapon.WeaponStats[stat] = Mathf.Max(1.0f, weapon.WeaponStats[stat]); break;
                        case StatType.CriticalChance: weapon.WeaponStats[stat] = Mathf.Max(0f, weapon.WeaponStats[stat]); break;
                        case StatType.MaxRange: weapon.WeaponStats[stat] = Mathf.Max(0.5f, weapon.WeaponStats[stat]); break;
                        case StatType.CritMultiplier:
                            // 暴击伤害倍率至少是 1.0 (即不加伤)，防止配置错误导致暴击反而没伤害
                            weapon.WeaponStats[stat] = Mathf.Max(1.0f, weapon.WeaponStats[stat]);
                            break;
                    }
                    return;
                }
            }
        }
        if (GlobalStats.ContainsKey(stat)) GlobalStats[stat] += delta;
        else GlobalStats.Add(stat, delta);
        switch (stat)
        {
            case StatType.AddedHP: MaxHP = Mathf.Max(1f, GetGlobalStat(StatType.AddedHP)); break;
            case StatType.AddedAP: MaxAP = Mathf.Max(0f, GetGlobalStat(StatType.AddedAP)); break;
            case StatType.PowerCost: TotalPowerCost = Mathf.Max(0f, GetGlobalStat(StatType.PowerCost)); break;
            case StatType.AddedMass: TotalMass = Mathf.Max(0.1f, GetGlobalStat(StatType.AddedMass)); break;
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