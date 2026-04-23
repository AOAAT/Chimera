using UnityEngine;

[CreateAssetMenu(fileName = "AreaDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成范围伤害 (Area Damage)")]
public class Action_AreaDamage : ECAAction
{
    [Range(0f, 3f)] public float DamageMultiplier = 1.0f;
    public float BonusRadius = 0f;
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        if (context == null || context.SourceWeapon == null) return;

        float baseRadius = context.SourceWeapon.GetStat(StatType.ExplosionRadius);
        float realRadius = (baseRadius + BonusRadius) * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);

        // 性能优化：使用距离平方比较
        float sqrRadius = realRadius * realRadius;
        float finalDmg = context.BaseDamage * DamageMultiplier;

        // 确定阵营列表
        var targets = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        // 暴力循环取代 LINQ
        for (int i = 0; i < targets.Count; i++)
        {
            var rec = targets[i];
            if (rec == null || rec.CurrentHP <= 0) continue;

            // 计算偏移向量的平方模长
            Vector3 offset = rec.transform.position - context.ImpactPoint;
            if (offset.sqrMagnitude <= sqrRadius)
            {
                rec.TakeDamage(finalDmg, context.SourceWeapon.WeaponName + " (溅射)", IsTrueDamage, context.IsCriticalHit);
            }
        }
    }
}