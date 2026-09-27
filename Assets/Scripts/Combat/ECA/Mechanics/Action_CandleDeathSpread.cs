// --- Action_CandleDeathSpread.cs (V2.0 加固版) ---
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CandleDeathSpread_V2", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 烛火死后全场传染 V2")]
public class Action_CandleDeathSpread : ECAAction
{
    [Header("=== 资源引用 ===")]
    public BuffDataSO CandleBuff;
    public GameObject ProjectilePrefab;
    public ComponentDataSO CandleWeaponSO;

    [Header("=== 传染参数 ===")]
    [Range(0.1f, 5.0f)] public float TransferRatio = 1.0f;
    [Range(0.1f, 2.0f)] public float DamageRatio = 0.5f;

    public Action_CandleDeathSpread() { Priority = 400; } // 属于后效层

    public override void Execute(ECAContext context)
    {
        // 1. 核心判空
        if (context.SourceEntity == null || ProjectilePrefab == null || CandleBuff == null || CandleWeaponSO == null) return;

        BuffManager deadBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (deadBuffMgr == null) return;

        // 2. 获取死者身上的烛火层数
        int stacks = deadBuffMgr.GetBuffStacks(CandleBuff.BuffID);
        if (stacks <= 0) return;

        // 3. 确定“传染者”阵营
        DamageReceiver deadReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        bool deadUnitIsEnemy = (deadReceiver != null) ? deadReceiver.isEnemy : true;

        // 4. 溯源：寻找该阵营中等级最高的烛火武器作为模版
        RuntimeWeapon masterWeapon = FindMasterCandleWeapon(!deadUnitIsEnemy);
        if (masterWeapon == null) return;

        // 5. 寻找受害者 (死者的队友)
        var potentialTargets = deadUnitIsEnemy ?
            CombatDirector.ActiveEnemies.Where(e => e != null && e.CurrentHP > 0 && e.transform != context.SourceEntity).ToList() :
            CombatDirector.ActivePlayerUnits.Where(p => p != null && p.CurrentHP > 0 && p.transform != context.SourceEntity).ToList();

        if (potentialTargets.Count == 0) return;

        // 6. 构造虚拟武器 (基于 Master 的数值)
        RuntimeWeapon infectorWeapon = ConstructInfectorWeapon(masterWeapon);

        // 7. 发射传染弹
        float weaponBaseDmg = masterWeapon.GetStat(StatType.MaxDamage);
        float finalBulletDamage = (weaponBaseDmg > 0 ? weaponBaseDmg : 10f) * DamageRatio;
        int totalProjectiles = Mathf.Max(1, Mathf.FloorToInt(stacks * TransferRatio));

        for (int i = 0; i < totalProjectiles; i++)
        {
            DamageReceiver target = potentialTargets[Random.Range(0, potentialTargets.Count)];

            // 随机旋转发射
            Quaternion randomRot = Quaternion.Euler(0, 0, Random.Range(0, 360f));
            GameObject projObj = SimplePool.Spawn(ProjectilePrefab, context.SourceEntity.position, randomRot);
            Projectile pScript = projObj.GetComponent<Projectile>();

            if (pScript != null)
            {
                // 构造传染上下文 (代际递增)
                ECAContext spreadCtx = new ECAContext
                {
                    SourceEntity = context.SourceEntity,
                    PrimaryTarget = target.transform,
                    ImpactPoint = context.SourceEntity.position,
                    SourceWeapon = infectorWeapon,
                    ChassisData = context.ChassisData,
                    IsEnemyFire = deadUnitIsEnemy,
                    BaseDamage = finalBulletDamage,
                    Generation = context.Generation + 1, // 🌟 代际增加，防止无限分裂
                    HitAllies = true // 传染是打同僚
                };

                pScript.FireV2(spreadCtx);
            }
        }
    }

    private RuntimeWeapon FindMasterCandleWeapon(bool isPlayerSide)
    {
        var owners = isPlayerSide ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        RuntimeWeapon highest = null;
        int maxLv = -1;

        foreach (var owner in owners)
        {
            var w = owner.GetComponentsInChildren<WeaponModule>()
                     .Select(m => m.GetWeaponData())
                     .FirstOrDefault(d => d != null && d.SourceSO == CandleWeaponSO);

            if (w != null && w.CurrentLevel > maxLv)
            {
                maxLv = w.CurrentLevel;
                highest = w;
            }
        }
        return highest;
    }

    private RuntimeWeapon ConstructInfectorWeapon(RuntimeWeapon master)
    {
        RuntimeWeapon dummy = new RuntimeWeapon
        {
            WeaponName = "遗志烛火",
            SourceSO = master.SourceSO,
            CurrentLevel = master.CurrentLevel,
            DeliveryType = WeaponDeliveryType.Ranged
        };

        // 镜像原始属性
        foreach (var statKey in master.WeaponStats.Keys)
            dummy.WeaponStats[statKey] = master.WeaponStats[statKey];

        // 👇【核心修复】：使用新的 ApplyBuffUniversal 类名
        Action_ApplyBuffUniversal applyInfection = ScriptableObject.CreateInstance<Action_ApplyBuffUniversal>();
        applyInfection.BuffToApply = CandleBuff;
        applyInfection.Priority = 310;
        dummy.OnHitActions.Add(applyInfection);

        // 增加基础损害积木
        Action_DealDamage damageAction = ScriptableObject.CreateInstance<Action_DealDamage>();
        damageAction.Priority = 300;
        dummy.OnHitActions.Add(damageAction);

        dummy.SortActions();
        return dummy;
    }
}