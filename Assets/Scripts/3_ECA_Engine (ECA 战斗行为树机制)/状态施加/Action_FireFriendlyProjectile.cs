using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "FireFriendlyProjectile", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 发射友方增益弹")]
public class Action_FireFriendlyProjectile : ECAAction
{
    [Header("=== 核心配置 ===")]
    public GameObject ProjectilePrefab;
    public float Range = 10f;
    public float ProjectileSpeed = 15f;

    [Header("=== 效果配置 ===")]
    public BuffDataSO BuffToApply;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null || BuffToApply == null) return;

        float realRange = CombatSandbox.GetDist(Range);

        // 【核心修复】：重新定义“谁是我的队友”
        // 如果我是敌机（IsEnemyFire = true），那我的队友列表就是 ActiveEnemies
        // 如果我是玩家机甲，我的队友就是 ActivePlayerUnits
        var allies = context.IsEnemyFire ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;

        var validTargets = allies.Where(a =>
            a != null &&
            a.CurrentHP > 0 &&
            a.transform != context.SourceEntity &&
            Vector3.Distance(context.SourceEntity.position, a.transform.position) <= realRange
        ).ToList();

        if (validTargets.Count == 0)
        {
            // Debug.Log($"[{context.SourceEntity.name}] 附近没有队友，取消增益弹发射。");
            return;
        }
        // 2. 随机抽取一个队友
        DamageReceiver target = validTargets[Random.Range(0, validTargets.Count)];

        // 3. 构造“针管”专用虚拟武器数据
        RuntimeWeapon needleWeapon = new RuntimeWeapon
        {
            WeaponName = "肾上腺注射器",
            ProjectilePrefab = this.ProjectilePrefab
        };
        needleWeapon.WeaponStats[StatType.ProjectileSpeed] = ProjectileSpeed;

        // 给这发子弹注入“命中即上Buff”的逻辑积木
        Action_ApplyBuffUniversal applyAction = ScriptableObject.CreateInstance<Action_ApplyBuffUniversal>();
        applyAction.BuffToApply = this.BuffToApply;
        applyAction.TargetMode = BuffTargetMode.Single;
        needleWeapon.OnHitActions.Add(applyAction);

        // 4. 正式发射
        GameObject projObj = SimplePool.Spawn(ProjectilePrefab, context.SourceEntity.position, Quaternion.identity);
        Projectile pScript = projObj.GetComponent<Projectile>();
        Debug.Log($"<color=#00FF00>【异变肾上腺】</color> 锁定队友 {target.name}，发射强效针剂！");
        if (pScript != null)
        {


            // 参数列表：目标, 伤害(0), 武器, 机甲黑盒, 开火者, 是否敌火, 是否暴击, 代际, 是否奶弹(true)
            // --- Action_FireFriendlyProjectile.cs 约第 65 行 ---
            pScript.Fire(
                target.transform,
                0f,
                needleWeapon,
                context.ChassisData,
                context.SourceEntity,
                context.IsEnemyFire,
                false,
                0,
                true,
                ProjectilePrefab // 👈 【修复】：传入积木面板上拖入的预制体
            );
        }
    }
}