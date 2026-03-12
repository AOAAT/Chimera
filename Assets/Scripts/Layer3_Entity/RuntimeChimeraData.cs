using System.Collections.Generic;
using UnityEngine;

// 武器的运行时实体数据
[System.Serializable]
public class RuntimeWeapon
{
    public string WeaponName;
    public Dictionary<StatType, float> WeaponStats = new Dictionary<StatType, float>();
    public List<ECABlock> WeaponMechanics = new List<ECABlock>();

    public WeaponDeliveryType DeliveryType;
    // 删掉 TargetType!
    public GameObject ProjectilePrefab;
    
    public List<ECAAction> OnFireActions = new List<ECAAction>(); // 新增插座
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    // 👇【新增】：动态临时暴击率池
    public float BonusCriticalChance = 0f;

    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }
}

// 奇美拉总机的运行时数据
[System.Serializable]
public class RuntimeChimeraData
{
    public string UnitName;

    // === 核心转化：将 Added 的累加值，转化为最终的战局绝对值 ===
    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }
    public float TotalPowerCost { get; private set; }
    public float TotalMass { get; private set; }
    public float TotalEnginePower { get; private set; }

    // 用于存储无法被硬编码的其他全局临时属性
    public Dictionary<StatType, float> GlobalStats = new Dictionary<StatType, float>();
    public HashSet<string> AllTags = new HashSet<string>();
    public List<ECABlock> GlobalMechanics = new List<ECABlock>();

    // === 武器实体列表：每一把武器都是独立的！ ===
    public List<RuntimeWeapon> EquippedWeapons = new List<RuntimeWeapon>();

    public void Assemble(ChassisDataSO chassis, ComponentDataSO[] components)
    {
        // 重置所有数据
        GlobalStats.Clear();
        AllTags.Clear();
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

            // 标签全局汇总
            foreach (var tag in comp.Tags) AllTags.Add(tag);
            if (comp.Type == ComponentType.Weapon)
            {
                RuntimeWeapon newWeapon = new RuntimeWeapon { WeaponName = comp.ComponentName };

                newWeapon.DeliveryType = comp.DeliveryType;
                newWeapon.ProjectilePrefab = comp.ProjectilePrefab;

                // 【之前加的】：把图纸里的“命中时”积木，装填到运行时的武器里！
                if (comp.OnHitActions != null)
                {
                    newWeapon.OnHitActions.AddRange(comp.OnHitActions);
                }

                // 👇【现在要加的】：把图纸里的“开火时”积木，也装填进去！
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

        // 3. 将累加的 GlobalStats 具象化为明确的面板数值
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);
    }

    private void ProcessStats(List<StatEntry> stats, bool isWeaponLocal, RuntimeWeapon weaponRef)
    {
        if (stats == null) return;

        foreach (var stat in stats)
        {
            // 判定该属性是 武器私有属性 还是 全局机甲属性
            if (isWeaponLocal && IsWeaponSpecificStat(stat.StatID))
            {
                // 存入独立武器的私有字典
                if (weaponRef.WeaponStats.ContainsKey(stat.StatID))
                    weaponRef.WeaponStats[stat.StatID] += stat.Value;
                else
                    weaponRef.WeaponStats.Add(stat.StatID, stat.Value);
            }
            else
            {
                // 注意：哪怕是武器，它的 PowerCost 和 AddedHP 依然要加给全局！
                if (GlobalStats.ContainsKey(stat.StatID))
                    GlobalStats[stat.StatID] += stat.Value;
                else
                    GlobalStats.Add(stat.StatID, stat.Value);
            }
        }
    }

    // 智能属性过滤器：决定哪些属性不能被融合
    // 智能属性过滤器：决定哪些属性不能被融合
    private bool IsWeaponSpecificStat(StatType type)
    {
        return type == StatType.MaxDamage ||
               type == StatType.MinDamage ||
               type == StatType.MaxRange ||
               type == StatType.MinRange ||
               type == StatType.AttackSpeed ||
               type == StatType.CriticalChance ||
               // 👇【核心修复 2】：把新加的三个子弹属性也加入武器的“私有财产”白名单！
               type == StatType.ExplosionRadius ||
               type == StatType.MultiShotCount ||
               type == StatType.ProjectileSpeed;
    }
    public float GetGlobalStat(StatType statID)
    {
        return GlobalStats.ContainsKey(statID) ? GlobalStats[statID] : 0f;
    }
}