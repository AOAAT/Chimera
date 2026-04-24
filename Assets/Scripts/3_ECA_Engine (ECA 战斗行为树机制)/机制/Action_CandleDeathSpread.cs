using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CandleDeathSpread", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 烛火死后传染")]
public class Action_CandleDeathSpread : ECAAction
{
    [Header("=== 资源引用 ===")]
    public BuffDataSO CandleBuff;
    public GameObject ProjectilePrefab;
    public ComponentDataSO CandleWeaponSO;

    [Header("=== 传染参数 ===")]
    [Tooltip("层数与弹丸数量的转化比")]
    [Range(0.1f, 2.0f)] public float TransferRatio = 0.5f;

    [Tooltip("传染弹继承武器基础伤害的比例 (1.0 = 100%)")]
    [Range(0.1f, 2.0f)] public float DamageRatio = 0.5f; // 👈 新增：伤害系数

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null || CandleBuff == null || CandleWeaponSO == null) return;

        BuffManager deadBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (deadBuffMgr == null) return;

        int stacks = deadBuffMgr.GetBuffStacks(CandleBuff.BuffID);
        if (stacks <= 0) return;

        // 1. 获取死者阵营
        DamageReceiver deadReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        bool deadUnitIsEnemy = deadReceiver != null ? deadReceiver.isEnemy : true;

        // 2. 寻找下个受害者
        var allies = deadUnitIsEnemy ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
        var nextVictim = allies
            .Where(e => e != null && e.CurrentHP > 0 && e.transform != context.SourceEntity)
            .OrderBy(e => Vector3.Distance(context.SourceEntity.position, e.transform.position))
            .FirstOrDefault();

        if (nextVictim == null) return;

        // 3. 回溯获取武器等级
        int currentWeaponLevel = 1;
        if (context.ChassisData != null)
        {
            var runtimeWeapon = context.ChassisData.EquippedWeapons.FirstOrDefault(w => w.SourceSO == CandleWeaponSO);
            if (runtimeWeapon != null) currentWeaponLevel = runtimeWeapon.CurrentLevel;
        }

        // 4. 构造虚拟武器数据（用于提取属性）
        RuntimeWeapon dummyWeapon = new RuntimeWeapon { WeaponName = "遗志烛火", SourceSO = CandleWeaponSO, CurrentLevel = currentWeaponLevel };
        var levelData = CandleWeaponSO.GetLevelData(currentWeaponLevel);
        if (levelData != null)
        {
            foreach (var entry in levelData.Stats) dummyWeapon.WeaponStats[entry.StatID] = entry.Value;
            if (levelData.OnHitActions != null) dummyWeapon.OnHitActions.AddRange(levelData.OnHitActions);
        }

        // --- 👇【核心修改：动态抓取伤害】---
        // 从图纸中抓取 MaxDamage，如果没有配攻击力，则给 10 点保底
        float weaponBaseDmg = dummyWeapon.GetStat(StatType.MaxDamage);
        if (weaponBaseDmg <= 0) weaponBaseDmg = 10f;

        // 计算最终每发火苗的威力：武器攻击力 * 伤害系数
        float finalBulletDamage = weaponBaseDmg * DamageRatio;
        // ----------------------------------

        // 5. 暴力喷射
        int bulletCount = Mathf.Max(1, Mathf.FloorToInt(stacks * TransferRatio));

        for (int i = 0; i < bulletCount; i++)
        {
            Quaternion randomRot = Quaternion.Euler(0, 0, Random.Range(0, 360f));
            GameObject projObj = SimplePool.Spawn(ProjectilePrefab, context.SourceEntity.position, randomRot);
            Projectile pScript = projObj.GetComponent<Projectile>();

            if (pScript != null)
            {
                pScript.Fire(
                    nextVictim.transform,
                    finalBulletDamage, // 👈 传入动态计算出的伤害
                    dummyWeapon,
                    context.ChassisData,
                    context.SourceEntity,
                    !deadUnitIsEnemy,
                    false,
                    0,
                    false,
                    ProjectilePrefab
                );
            }
        }
    }
}