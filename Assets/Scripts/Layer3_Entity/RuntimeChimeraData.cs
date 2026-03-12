using System.Collections.Generic;
using UnityEngine;

// 武器的运行时实体数据
[System.Serializable]
public class RuntimeWeapon
{
    public string WeaponName;
    public ComponentDataSO SourceSO; // 【溯源指针】：记下自己是由哪张图纸变来的
    public Dictionary<StatType, float> WeaponStats = new Dictionary<StatType, float>();
    public List<ECABlock> WeaponMechanics = new List<ECABlock>();

    public WeaponDeliveryType DeliveryType;
    public GameObject ProjectilePrefab;

    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    public float BonusCriticalChance = 0f;

    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }
}

// 奇美拉总机的运行时数据
[System.Serializable]
public class RuntimeChimeraData
{
    public string UnitName;

    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }
    public float TotalPowerCost { get; private set; }
    public float TotalMass { get; private set; }
    public float TotalEnginePower { get; private set; }

    public Dictionary<StatType, float> GlobalStats = new Dictionary<StatType, float>();
    public List<ComponentTag> Tags = new List<ComponentTag>(); // 强类型标签列表
    public List<ECABlock> GlobalMechanics = new List<ECABlock>();
    public List<RuntimeWeapon> EquippedWeapons = new List<RuntimeWeapon>();

    // 👇【核心修复 1】：添加了图纸清单列表！解决 CS1061 报错
    public List<ComponentDataSO> AllEquippedSOs = new List<ComponentDataSO>();

    public void Assemble(ChassisDataSO chassis, ComponentDataSO[] components)
    {
        // 👇【核心修复 1 续】：在组装一开始，将传入的组件数组存入图纸清单
        AllEquippedSOs = new List<ComponentDataSO>(components);

        // 重置所有数据
        GlobalStats.Clear();
        Tags.Clear(); // 👇【核心修复 2】：你原本写成了 AllTags.Clear()，导致报错，现已统一！
        GlobalMechanics.Clear();
        EquippedWeapons.Clear();
        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;

        if (chassis == null) return;
        UnitName = chassis.ChassisName;

        // 1. 结算底盘基础
        ProcessStats(chassis.BaseStats, isWeaponLocal: false, null);

        // 2. 结算组件
        foreach (var comp in components)
        {
            if (comp == null) continue;

            // 标签全局汇总 (加入空值保护)
            if (comp.Tags != null)
            {
                foreach (var tag in comp.Tags) Tags.Add(tag); // 👇【核心修复 2 续】：统一使用 Tags 列表
            }

            if (comp.Type == ComponentType.Weapon)
            {
                RuntimeWeapon newWeapon = new RuntimeWeapon { WeaponName = comp.ComponentName };
                newWeapon.SourceSO = comp;
                newWeapon.DeliveryType = comp.DeliveryType;
                newWeapon.ProjectilePrefab = comp.ProjectilePrefab;

                if (comp.OnHitActions != null)
                {
                    newWeapon.OnHitActions.AddRange(comp.OnHitActions);
                }

                if (comp.OnFireActions != null)
                {
                    newWeapon.OnFireActions.AddRange(comp.OnFireActions);
                }

                if (comp.ECA_Mechanics != null) newWeapon.WeaponMechanics.AddRange(comp.ECA_Mechanics);
                ProcessStats(comp.BaseStats, isWeaponLocal: true, newWeapon);
                EquippedWeapons.Add(newWeapon);
            }
            else
            {
                // 其他组件（核心、辅助、移动），所有机制和属性归属全局
                if (comp.ECA_Mechanics != null) GlobalMechanics.AddRange(comp.ECA_Mechanics);
                ProcessStats(comp.BaseStats, isWeaponLocal: false, null);
            }
        }
        // 👇【核心修复】：在触发 ECA 光环之前，先把字典里的基础数值“倒”给面板变量！
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);

        // 3. 触发装配期 ECA 光环！
        foreach (var comp in components)
        {
            if (comp != null && comp.OnAssembleActions != null && comp.OnAssembleActions.Count > 0)
            {
                ECAContext assembleContext = new ECAContext
                {
                    ChassisData = this,
                    SourceComponentSO = comp
                };

                foreach (var action in comp.OnAssembleActions)
                {
                    if (action != null) action.Execute(assembleContext);
                }
            }
        }
    }

    // 修饰后门：供 ECA 积木调用，极其精准地修改全局或局部数值
    public void ModifyStat(ComponentDataSO targetSO, StatType stat, float delta)
    {
        // 1. 安检：必须先判断这个属性是不是“武器的私有财产”（如伤害、射程）
        if (IsWeaponSpecificStat(stat))
        {
            foreach (var weapon in EquippedWeapons)
            {
                if (weapon.SourceSO == targetSO)
                {
                    if (weapon.WeaponStats.ContainsKey(stat)) weapon.WeaponStats[stat] += delta;
                    else weapon.WeaponStats[stat] = delta;
                    return; // 局部修改完毕，安全撤退
                }
            }
        }

        // 2. 如果走到这里，说明是全局属性（如耗电量、血量），或者是辅助组件的属性
        // 无论它是谁提供的，一律修改机甲总闸！
        if (stat == StatType.PowerCost) TotalPowerCost += delta;
        else if (stat == StatType.AddedHP) MaxHP += delta;
        else if (stat == StatType.AddedAP) MaxAP += delta;
        else if (stat == StatType.AddedMass) TotalMass += delta;
        else if (stat == StatType.EnginePower) TotalEnginePower += delta;

        // 3. 【极其关键】：同步更新 GlobalStats 字典！
        // 这样无论你的“同步代码”写在 ECA 的前面还是后面，拿到的永远是最新数据！
        if (GlobalStats.ContainsKey(stat)) GlobalStats[stat] += delta;
        else GlobalStats.Add(stat, delta);
    }

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