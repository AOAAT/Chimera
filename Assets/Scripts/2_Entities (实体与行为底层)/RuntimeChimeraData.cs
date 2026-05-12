using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// ==========================================
// 1. 运行时武器容器 (ECA 2.0 加固版)
// ==========================================
[System.Serializable]
public class RuntimeWeapon
{
    public string WeaponName;
    public ComponentDataSO SourceSO;
    public int CurrentLevel = 1;
    public Dictionary<StatType, float> WeaponStats = new Dictionary<StatType, float>();
    public WeaponDeliveryType DeliveryType;
    public GameObject ProjectilePrefab;
    public float BonusCriticalChance = 0f;
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();

    // 动态动作池
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }

    // 🚀 ECA 2.0 核心：自排序协议
    public void SortActions()
    {
        OnFireActions.Sort((a, b) => (a != null && b != null) ? a.Priority.CompareTo(b.Priority) : 0);
        OnHitActions.Sort((a, b) => (a != null && b != null) ? a.Priority.CompareTo(b.Priority) : 0);
    }

    // 🚀 ECA 2.0 核心：命中分发协议
    public void TriggerHitPipeline(Transform target, Vector3 hitPos, ECAContext fireCtx)
    {
        if (target == null) return;

        // 构造继承开火加成的命中上下文
        ECAContext hitCtx = new ECAContext
        {
            SourceEntity = fireCtx.SourceEntity,
            PrimaryTarget = target,
            ImpactPoint = hitPos,
            SourceWeapon = this,
            ChassisData = fireCtx.ChassisData,
            BaseDamage = fireCtx.BaseDamage,
            TemporaryDamageModifier = fireCtx.TemporaryDamageModifier,
            TemporaryCritModifier = fireCtx.TemporaryCritModifier,
            IsCriticalHit = fireCtx.IsCriticalHit,
            IsEnemyFire = fireCtx.IsEnemyFire,
            Generation = fireCtx.Generation, // 传递代际
            HitAllies = fireCtx.HitAllies      // 传递过滤标签
        };

        foreach (var action in OnHitActions)
        {
            if (action == null || hitCtx.ExecutionAborted) break;
            action.Execute(hitCtx);
        }
    }
}

// ==========================================
// 2. 运行时机甲黑盒 (ECA 2.0 驱动核心)
// ==========================================
[System.Serializable]
public class RuntimeChimeraData
{
    public string UnitID;
    public string UnitName;
    public ChassisDataSO ActiveChassisSO;
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
    public Dictionary<ComponentDataSO, Dictionary<StatType, float>> ComponentLocalOffsets = new Dictionary<ComponentDataSO, Dictionary<StatType, float>>();

    public List<SubTag> Tags = new List<SubTag>();
    public List<RuntimeWeapon> EquippedWeapons = new List<RuntimeWeapon>();
    public List<ComponentDataSO> AllEquippedSOs = new List<ComponentDataSO>();

    // 全局管线池
    public List<ECAAction> GlobalOnFireActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnHitActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnBattleStartActions = new List<ECAAction>();
    public List<ECAAction> GlobalOnTickActions = new List<ECAAction>(); // 👈 ECA 2.0 周期管线

    public ActiveSkillConfig CoreActiveSkill;
    public bool CanFireWhileManualMoving = false;

    // ==========================================
    // 🚀 核心重构：大一统装配管线 V2.0
    // ==========================================
    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components, Transform entityTransform = null)
    {
        // --- 1. 物理隔离：清理旧数据 ---
        GlobalOnFireActions.Clear();
        GlobalOnHitActions.Clear();
        GlobalOnBattleStartActions.Clear();
        GlobalOnTickActions.Clear();
        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        ComponentLocalOffsets.Clear();
        EquippedWeapons.Clear();
        Tags.Clear();

        MaxHP = MaxAP = TotalPowerCost = TotalMass = TotalEnginePower = 0;
        this.ActiveChassisSO = chassis;

        if (chassis == null) return;

        // --- 2. 基础登记：底盘载入 ---
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;
        ProcessStats(chassis.BaseStats, false, null);

        // --- 3. 核心循环：零件与配件注入 ---
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var compSO = compInstance.BaseData;
            AllEquippedSOs.Add(compSO);

            var levelData = compSO.GetLevelData(compInstance.CurrentLevel);
            if (levelData == null) continue;

            // 标签合并
            if (compSO.BaseSubTags != null) Tags.AddRange(compSO.BaseSubTags);
            if (levelData.BonusTags != null) Tags.AddRange(levelData.BonusTags);

            // 分流处理：武器 vs 非武器
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

                // A. 注入零件原生积木
                if (levelData.OnFireActions != null) newWeapon.OnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.OnHitActions != null) newWeapon.OnHitActions.AddRange(levelData.OnHitActions);

                // B. 👇【配件地基】：注入插槽配件积木
                // 未来在这里呼叫配件管理器，根据 SocketedAccessoryIDs 获取积木并 AddRange
                InjectAccessoryActions(compInstance, newWeapon);

                EquippedWeapons.Add(newWeapon);
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
                ProcessStats(levelData.Stats, false, null);
            }

            // C. 收集全局效果（含 OnTick 心跳）
            if (compSO.Type != ComponentType.Weapon)
            {
                if (levelData.OnFireActions != null) GlobalOnFireActions.AddRange(levelData.OnFireActions);
                if (levelData.OnHitActions != null) GlobalOnHitActions.AddRange(levelData.OnHitActions);
                if (levelData.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(levelData.OnBattleStartActions);
            }
            if (levelData.OnTickActions != null) GlobalOnTickActions.AddRange(levelData.OnTickActions);
        }

        // --- 4. 逻辑激活：执行装配动作 ---
        // 底盘装配动作先行
        if (chassis.OnAssembleActions != null)
        {
            ECAContext chassisCtx = new ECAContext { ChassisData = this, SourceEntity = entityTransform };
            foreach (var a in chassis.OnAssembleActions) if (a != null) a.Execute(chassisCtx);
        }
        if (chassis.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(chassis.OnBattleStartActions);

        // 零件装配动作跟进
        foreach (var compInstance in components)
        {
            if (compInstance == null) continue;
            var levelData = compInstance.BaseData.GetLevelData(compInstance.CurrentLevel);
            if (levelData != null && levelData.OnAssembleActions != null)
            {
                ECAContext compCtx = new ECAContext { ChassisData = this, SourceComponentSO = compInstance.BaseData, SourceEntity = entityTransform };
                foreach (var a in levelData.OnAssembleActions) if (a != null) a.Execute(compCtx);
            }
        }

        // --- 5. 序列重整：全管线优先级排序 ---
        foreach (var w in EquippedWeapons) w.SortActions();
        GlobalOnFireActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnHitActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnBattleStartActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnTickActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // --- 6. 最终解算 ---
        RefreshFinalStats();
    }

    // 👇【配件系统占位接口】
    private void InjectAccessoryActions(InstancedComponent comp, RuntimeWeapon runtimeWpn)
    {
        // 这里的逻辑在未来配件系统实现后补全：
        // foreach(var accID in comp.SocketedAccessoryIDs) { ... }
    }

    private void ProcessStats(List<StatEntry> stats, bool isWeaponLocal, RuntimeWeapon weaponRef)
    {
        if (stats == null) return;
        foreach (var stat in stats)
        {
            if (isWeaponLocal && weaponRef != null && IsWeaponSpecificStat(stat.StatID))
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
            if (!ComponentLocalOffsets.ContainsKey(targetSO)) ComponentLocalOffsets.Add(targetSO, new Dictionary<StatType, float>());
            if (ComponentLocalOffsets[targetSO].ContainsKey(stat)) ComponentLocalOffsets[targetSO][stat] += delta;
            else ComponentLocalOffsets[targetSO].Add(stat, delta);

            var weapon = EquippedWeapons.Find(w => w.SourceSO == targetSO);
            if (weapon != null)
            {
                if (weapon.WeaponStats.ContainsKey(stat)) weapon.WeaponStats[stat] += delta;
                else weapon.WeaponStats.Add(stat, delta);
            }
        }

        if (GlobalStats.ContainsKey(stat)) GlobalStats[stat] += delta;
        else GlobalStats.Add(stat, delta);

        RefreshFinalStats();
    }

    private void RefreshFinalStats()
    {
        MaxHP = GetGlobalStat(StatType.AddedHP);
        MaxAP = GetGlobalStat(StatType.AddedAP);
        TotalMass = GetGlobalStat(StatType.AddedMass);
        TotalEnginePower = GetGlobalStat(StatType.EnginePower);
        TotalPowerCost = GetGlobalStat(StatType.PowerCost);
    }

    private bool IsWeaponSpecificStat(StatType type) => (int)type >= 10 && (int)type <= 19;
    public float GetGlobalStat(StatType statID) => GlobalStats.ContainsKey(statID) ? GlobalStats[statID] : 0f;
}