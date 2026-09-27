using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ChainDamage_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 逻辑连锁 V2")]
public class Action_ChainDamage : ECAAction
{
    [Header("=== 连锁核心配置 ===")]
    [Tooltip("最高允许连锁到第几代？(0代表不连锁，1代表跳跃一次，以此类推)")]
    public int MaxGeneration = 1;

    public int MaxTargetsPerJump = 2;
    public float ChainRadius = 5f;

    [Range(0f, 1f)]
    [Tooltip("每一代相对于前一代的伤害衰减")]
    public float DamageDecay = 0.7f;

    [Header("=== 视觉表现 ===")]
    public GameObject LightningPrefab;
    public float LightningDuration = 0.2f;

    // 🌟 设置优先级为 400 (属于后效层)，确保主目标的伤害扣除已经完成
    public Action_ChainDamage() { Priority = 400; }

    public override void Execute(ECAContext context)
    {
        // 1. 【逻辑锁】：如果当前代际已经达到或超过上限，强行熔断，防止无限套娃
        if (context.Generation >= MaxGeneration) return;

        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 2. 获取沙盒缩放后的真实半径
        float realRadius = CombatSandbox.GetDist(ChainRadius);

        // 3. 寻找潜在受害者 (利用 CombatDirector 的静态列表，性能极高)
        var pool = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        var nextTargets = pool
            .Where(r => r != null && r.CurrentHP > 0 && r.transform != context.PrimaryTarget) // 排除自己
            .Where(r => Vector3.Distance(context.ImpactPoint, r.transform.position) <= realRadius)
            .OrderBy(r => Vector3.Distance(context.ImpactPoint, r.transform.position)) // 就近原则
            .Take(MaxTargetsPerJump).ToList();

        if (nextTargets.Count == 0) return;

        // 4. 【管线分发】：为每个连锁目标开启新的人生（新的 Context）
        foreach (var target in nextTargets)
        {
            // 绘制闪电视觉
            DrawLightning(context.ImpactPoint, target.transform.position);

            // --- 👇【核心注入】：构造下一代 Context ---
            ECAContext nextCtx = new ECAContext
            {
                SourceEntity = context.SourceEntity,
                PrimaryTarget = target.transform,
                ImpactPoint = target.transform.position,
                SourceWeapon = context.SourceWeapon,
                ChassisData = context.ChassisData,
                IsEnemyFire = context.IsEnemyFire,

                // 核心逻辑接力：
                Generation = context.Generation + 1, // 🌟 代际递增
                BaseDamage = context.BaseDamage * DamageDecay, // 🌟 伤害衰减

                // 继承修饰符
                TemporaryDamageModifier = context.TemporaryDamageModifier,
                TemporaryCritModifier = context.TemporaryCritModifier,
                CustomStates = context.CustomStates
            };

            // 🚀 重新调用来源武器的命中管线！
            // 这会导致该武器上插的所有 OnHit 积木（如：腐蚀、吸血）在这个新目标上重新跑一遍
            context.SourceWeapon.TriggerHitPipeline(target.transform, target.transform.position, nextCtx);
        }
    }

    private void DrawLightning(Vector3 start, Vector3 end)
    {
        if (LightningPrefab == null) return;
        GameObject lightningObj = Instantiate(LightningPrefab, start, Quaternion.identity);
        LaserBeam laser = lightningObj.GetComponent<LaserBeam>();
        if (laser != null) laser.Fire(start, end, LightningDuration);
    }
}