using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CandleDeathSpread", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 烛火死后全场传染(溯源版)")]
public class Action_CandleDeathSpread : ECAAction
{
    [Header("=== 资源引用 ===")]
    public BuffDataSO CandleBuff;
    public GameObject ProjectilePrefab;
    public ComponentDataSO CandleWeaponSO;

    [Header("=== 传染参数 ===")]
    [Range(0.1f, 5.0f)] public float TransferRatio = 1.0f;
    [Range(0.1f, 2.0f)] public float DamageRatio = 0.5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null || CandleBuff == null || CandleWeaponSO == null) return;

        BuffManager deadBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (deadBuffMgr == null) return;

        int stacks = deadBuffMgr.GetBuffStacks(CandleBuff.BuffID);
        if (stacks <= 0) return;

        // 1. 确定谁才是“凶手”阵营 (如果死的是怪，凶手就是玩家)
        DamageReceiver deadReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        bool deadUnitIsEnemy = (deadReceiver != null) ? deadReceiver.isEnemy : true;

        // 2. 👇【核心溯源】：寻找凶手阵营中等级最高的烛火武器
        RuntimeWeapon masterWeapon = FindMasterCandleWeapon(!deadUnitIsEnemy);
        if (masterWeapon == null)
        {
            Debug.LogWarning("【烛火中断】全场找不到来源武器，无法产生传染苗。");
            return;
        }

        // 3. 构造虚拟武器 (基于凶手的最高等级)
        RuntimeWeapon infectorWeapon = ConstructInfectorWeapon(masterWeapon);

        // 4. 寻找下一批同僚受害者 (死者的队友)
        var potentialTargets = deadUnitIsEnemy ?
            CombatDirector.ActiveEnemies.Where(e => e != null && e.CurrentHP > 0 && e.transform != context.SourceEntity).ToList() :
            CombatDirector.ActivePlayerUnits.Where(p => p != null && p.CurrentHP > 0 && p.transform != context.SourceEntity).ToList();

        if (potentialTargets.Count == 0) return;

        // 5. 计算弹丸与发射
        float weaponBaseDmg = infectorWeapon.GetStat(StatType.MaxDamage);
        float finalBulletDamage = (weaponBaseDmg > 0 ? weaponBaseDmg : 10f) * DamageRatio;
        int totalProjectiles = Mathf.Max(1, Mathf.FloorToInt(stacks * TransferRatio));

        for (int i = 0; i < totalProjectiles; i++)
        {
            DamageReceiver target = potentialTargets[Random.Range(0, potentialTargets.Count)];
            Quaternion randomRot = Quaternion.Euler(0, 0, Random.Range(0, 360f));
            GameObject projObj = SimplePool.Spawn(ProjectilePrefab, context.SourceEntity.position, randomRot);
            Projectile pScript = projObj.GetComponent<Projectile>();

            if (pScript != null)
            {
                pScript.Fire(
                    target.transform,
                    finalBulletDamage,
                    infectorWeapon,
                    null, // 此时由于是死后触发，不再强挂 ChassisData
                    context.SourceEntity,
                    deadUnitIsEnemy,  // 维持死者的阵营层级，确保物理碰撞能打到它的队友
                    false,
                    0,
                    true,             // 允许命中同僚
                    ProjectilePrefab
                );
            }
        }
    }

    /// <summary>
    /// 【全场扫描】：找到指定阵营中等级最高的烛火武器
    /// </summary>
    private RuntimeWeapon FindMasterCandleWeapon(bool isEnemySide)
    {
        // 根据阵营获取所有可能的持有者
        var owners = isEnemySide ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;

        RuntimeWeapon highestLevelWeapon = null;
        int maxLv = -1;

        foreach (var owner in owners)
        {
            // 尝试通过 MechUnit2D 或直接访问运行时数据获取武器列表
            // 无论是玩家机甲还是精英怪机甲，都挂有 MechUnit2D 
            var mech = owner.GetComponent<MechUnit2D>();
            if (mech == null) continue;

            // 这里需要我们能访问到 MechUnit2D 里的 cachedCombatData
            // (主程：如果 cachedCombatData 是私有的，建议将其改为 internal 或 public，或者加个 getter)

            // 暴力搜索该单位身上所有的武器
            // 我们通过导出接口获取：
            var rw = owner.GetComponentsInChildren<WeaponModule>()
                     .Select(w => w.GetWeaponData()) // 假设 WeaponModule 暴露了数据
                     .FirstOrDefault(d => d.SourceSO == CandleWeaponSO);

            if (rw != null && rw.CurrentLevel > maxLv)
            {
                maxLv = rw.CurrentLevel;
                highestLevelWeapon = rw;
            }
        }
        return highestLevelWeapon;
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

        // 直接镜像最高等级武器的数值
        foreach (var statKey in master.WeaponStats.Keys)
            dummy.WeaponStats[statKey] = master.WeaponStats[statKey];

        // 注入扣血与上Buff双积木
        Action_DealDamage damageAction = ScriptableObject.CreateInstance<Action_DealDamage>();
        dummy.OnHitActions.Add(damageAction);

        Action_ApplyBuff applyInfection = ScriptableObject.CreateInstance<Action_ApplyBuff>();
        applyInfection.BuffToApply = CandleBuff;
        dummy.OnHitActions.Add(applyInfection);

        return dummy;
    }
}