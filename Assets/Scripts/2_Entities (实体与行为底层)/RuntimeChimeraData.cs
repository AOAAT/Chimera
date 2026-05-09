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

    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components, Transform entityTransform = null)
    {
        // 1. 初始化清空
        GlobalOnFireActions.Clear();
        GlobalOnHitActions.Clear();
        GlobalOnKillActions.Clear();
        GlobalOnBattleStartActions.Clear();
        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        Tags.Clear();
        EquippedWeapons.Clear();
        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;
        CoreActiveSkill = null;

        if (chassis == null) return;


        this.ActiveChassisSO = chassis;
        // 2. 底盘基础登记
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;
        ProcessStats(chassis.BaseStats, false, null);

        // 3. 第一遍循环：登记所有零件
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var compSO = compInstance.BaseData;
            AllEquippedSOs.Add(compSO);

            var levelData = compSO.GetLevelData(compInstance.CurrentLevel);
            if (levelData == null) continue;

            // 标签合并
            if (compSO.BaseSubTags != null) foreach (var tag in compSO.BaseSubTags) Tags.Add(tag);
            if (levelData.BonusTags != null) foreach (var tag in levelData.BonusTags) Tags.Add(tag);

            if (compSO.Type == ComponentType.Weapon)
            {
                RuntimeWeapon newWeapon = new RuntimeWeapon
                {
                    WeaponName = compSO.ComponentName,
                    SourceSO = compSO,
                    CurrentLevel = compInstance.CurrentLevel,
                    DeliveryType = compSO.DeliveryType,
                    ProjectilePrefab = compSO.ProjectilePrefab
                };
                // 【判空保护】：OnHit/OnFire 列表可能为 null
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

            // 收集全局效果
            if (compSO.Type != ComponentType.Weapon)
            {
                if (levelData.OnFireActions != null) GlobalOnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.OnHitActions != null) GlobalOnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(levelData.OnBattleStartActions);
            }
        }

        // 4. 第二遍循环：逻辑触发
        // --- A. 底盘积木 (加固判空) ---
        if (chassis.OnAssembleActions != null && chassis.OnAssembleActions.Count > 0)
        {
            ECAContext chassisCtx = new ECAContext { ChassisData = this, SourceEntity = entityTransform };
            foreach (var action in chassis.OnAssembleActions)
                if (action != null) action.Execute(chassisCtx);
        }

        // 底盘开战协议
        if (chassis.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(chassis.OnBattleStartActions);

        // --- B. 零件积木 (加固判空) ---
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var levelData = compInstance.BaseData.GetLevelData(compInstance.CurrentLevel);
            if (levelData == null) continue;

            // 👇【核心报错修复点】：检查列表是否为 null
            if (levelData.OnAssembleActions != null && levelData.OnAssembleActions.Count > 0)
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

        RefreshFinalStats();
    }

    private void RefreshFinalStats()
    {
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);
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