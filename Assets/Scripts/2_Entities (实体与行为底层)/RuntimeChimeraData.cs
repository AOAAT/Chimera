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

    // ECA 2.0 动作池
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    // 🌟【新增】：心跳动作池
    public List<ECAAction> OnTickActions = new List<ECAAction>();

    public float GetStat(StatType statID) { return WeaponStats.ContainsKey(statID) ? WeaponStats[statID] : 0f; }

    public void SortActions()
    {
        // 使用 LINQ 进行空值过滤并排序
        OnFireActions = OnFireActions.Where(a => a != null).OrderBy(a => a.Priority).ToList();
        OnHitActions = OnHitActions.Where(a => a != null).OrderBy(a => a.Priority).ToList();

        // 🌟【新增】：别忘了心跳管线也要排序！
        if (OnTickActions != null)
        {
            OnTickActions = OnTickActions.Where(a => a != null).OrderBy(a => a.Priority).ToList();
        }
    }

    public void TriggerHitPipeline(Transform target, Vector3 hitPos, ECAContext fireCtx)
    {
        if (target == null) return;
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
            Generation = fireCtx.Generation,
            HitAllies = fireCtx.HitAllies,
            CustomStates = fireCtx.CustomStates // 🌟 维持字典链条
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
    public Dictionary<string, float> PersistentStates = new Dictionary<string, float>();
    public ActiveSkillConfig CoreActiveSkill;
    public bool CanFireWhileManualMoving = false;
 
    public Dictionary<ComponentDataSO, RuntimeWeapon> ComponentToRuntimeMap = new Dictionary<ComponentDataSO, RuntimeWeapon>();
    // ==========================================
    // 🚀 核心重构：大一统装配管线 V2.0 (全量加固版)
    // ==========================================
    // --- RuntimeChimeraData.cs ---

    public void Assemble(ChassisDataSO chassis, InstancedComponent[] components, Transform entityTransform = null)
    {
        // --- 1. 初始化清理 ---
        GlobalOnFireActions.Clear();
        GlobalOnHitActions.Clear();
        GlobalOnBattleStartActions.Clear();
        GlobalOnTickActions.Clear();
        AllEquippedSOs.Clear();
        GlobalStats.Clear();
        ComponentLocalOffsets.Clear();
        ComponentToRuntimeMap.Clear();
        EquippedWeapons.Clear();
        Tags.Clear();

        MaxHP = MaxAP  = TotalMass = TotalEnginePower = 0;
        this.ActiveChassisSO = chassis;

        if (chassis == null) return;

        // --- 2. 载入底盘基础 ---
        UnitName = chassis.ChassisName;
        LogicCenterOffset = chassis.LogicCenterOffset;
        ProcessStats(chassis.BaseStats, false, null);

        // --- 3. 核心零件解析循环 ---
        foreach (var compInstance in components)
        {
            if (compInstance == null || compInstance.BaseData == null) continue;

            var compSO = compInstance.BaseData;
            AllEquippedSOs.Add(compSO);

            var levelData = compSO.GetModelData(compInstance.CurrentMark);
            if (levelData == null) continue;

            // A. 创建逻辑代理
            RuntimeWeapon runtimeProxy = new RuntimeWeapon
            {
                WeaponName = compSO.ComponentName,
                SourceSO = compSO,
                CurrentLevel = compInstance.CurrentMark,
                DeliveryType = compSO.DeliveryType,
                ProjectilePrefab = compSO.ProjectilePrefab
            };

            // B. 全量拷贝积木管线 (底稿)
            if (levelData.OnHitActions != null) runtimeProxy.OnHitActions = new List<ECAAction>(levelData.OnHitActions);
            if (levelData.OnFireActions != null) runtimeProxy.OnFireActions = new List<ECAAction>(levelData.OnFireActions);
            if (levelData.OnTickActions != null) runtimeProxy.OnTickActions = new List<ECAAction>(levelData.OnTickActions);

            // --- 👇【核心新增】：配件积木注入 ---
            // 在这里，我们将芯片里的逻辑“缝合”进刚刚拷贝好的 runtimeProxy 管线中
            InjectAccessoryData(compInstance, runtimeProxy);
            // ------------------------------------

            ComponentToRuntimeMap[compSO] = runtimeProxy;

            // C. 数值与全局收集
            if (compSO.Type == ComponentType.Weapon)
            {
                EquippedWeapons.Add(runtimeProxy);
                ProcessStats(levelData.Stats, true, runtimeProxy);
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
            if (levelData.OnTickActions != null) GlobalOnTickActions.AddRange(levelData.OnTickActions);
        }

        // --- 4. 逻辑动作执行与大排序 ---
        ExecuteAssembleActions(chassis, components, entityTransform);

        // 🌟 核心：注入完成后，必须进行大排序，确保芯片积木（配件）按照 Priority 正确插队
        foreach (var proxy in ComponentToRuntimeMap.Values) proxy.SortActions();

        GlobalOnFireActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnHitActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnBattleStartActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        GlobalOnTickActions.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        RefreshFinalStats();
    }

    /// <summary>
    /// 【配件注入工具】：将插槽芯片内的逻辑与数值压入运行时代理
    /// </summary>


    // 辅助方法：分离 Assemble 积木执行逻辑，保持主方法清晰
    private void ExecuteAssembleActions(ChassisDataSO chassis, InstancedComponent[] components, Transform entityTransform)
    {
        if (chassis.OnAssembleActions != null)
        {
            ECAContext chassisCtx = new ECAContext { ChassisData = this, SourceEntity = entityTransform };
            foreach (var a in chassis.OnAssembleActions) if (a != null) a.Execute(chassisCtx);
        }
        if (chassis.OnBattleStartActions != null) GlobalOnBattleStartActions.AddRange(chassis.OnBattleStartActions);

        foreach (var compInstance in components)
        {
            if (compInstance == null) continue;
            var levelData = compInstance.BaseData.GetModelData(compInstance.CurrentMark);
            if (levelData != null && levelData.OnAssembleActions != null)
            {
                ECAContext compCtx = new ECAContext { ChassisData = this, SourceComponentSO = compInstance.BaseData, SourceEntity = entityTransform };
                foreach (var a in levelData.OnAssembleActions) if (a != null) a.Execute(compCtx);
            }
        }
    }
    private void InjectAccessoryData(InstancedComponent compInstance, RuntimeWeapon runtimeProxy)
    {
        if (compInstance.SocketedAccessoryIDs == null || compInstance.SocketedAccessoryIDs.Count == 0) return;

        foreach (string accID in compInstance.SocketedAccessoryIDs)
        {
            var accessory = PlayerInventoryManager.Instance.GetAccessoryInstance(accID);
            if (accessory == null || accessory.BaseData == null) continue;

            AccessoryDataSO data = accessory.BaseData;

            // 1. 静态数值注入 (保持不变)
            if (data.StaticStatModifiers != null)
                ProcessStats(data.StaticStatModifiers, true, runtimeProxy);

            // 2. 精准管线注入
            // -----------------------------------------------------
            // A. 注入战斗管线 (绑定在零件代理上)
            if (data.InjectedOnFireActions != null) runtimeProxy.OnFireActions.AddRange(data.InjectedOnFireActions);
            if (data.InjectedOnHitActions != null) runtimeProxy.OnHitActions.AddRange(data.InjectedOnHitActions);
            if (data.InjectedOnTickActions != null) runtimeProxy.OnTickActions.AddRange(data.InjectedOnTickActions);

            // B. 注入初始化管线 (绑定在全局池中，随零件生效)
            if (data.InjectedOnAssembleActions != null)
            {
                // 注意：Assemble 积木由于是瞬时触发，在这里需要立即手动执行一次，或者加入全局列表
                foreach (var action in data.InjectedOnAssembleActions)
                {
                    if (action == null) continue;
                    // 构造一个临时的 Context 执行初始化加成
                    ECAContext ctx = new ECAContext { ChassisData = this, SourceComponentSO = compInstance.BaseData };
                    action.Execute(ctx);
                }
            }

            if (data.InjectedOnBattleStartActions != null)
            {
                // 将配件的开战逻辑合并到机甲的全局开战序列中
                GlobalOnBattleStartActions.AddRange(data.InjectedOnBattleStartActions);
            }
            // -----------------------------------------------------
        }
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
    }

    private bool IsWeaponSpecificStat(StatType type) => (int)type >= 10 && (int)type <= 19;
    public float GetGlobalStat(StatType statID) => GlobalStats.ContainsKey(statID) ? GlobalStats[statID] : 0f;
}