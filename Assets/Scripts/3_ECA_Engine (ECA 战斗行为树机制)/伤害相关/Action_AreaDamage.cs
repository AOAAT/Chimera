// --- START OF FILE Action_AreaDamage.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "AreaDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成范围伤害 (Area Damage)")]
public class Action_AreaDamage : ECAAction
{
    [Range(0f, 3f)] public float DamageMultiplier = 1.0f;
    public float BonusRadius = 0f;
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        float baseRadius = context.SourceWeapon.GetStat(StatType.ExplosionRadius);
        float realRadius = (baseRadius + BonusRadius) * CombatSandbox.Instance.DistanceMultiplier;
        float finalDmg = context.BaseDamage * DamageMultiplier;

        // 【优化】：不再 FindObjectsOfType，直接检索对应阵营列表
        var targets = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        for (int i = targets.Count - 1; i >= 0; i--) // 倒序遍历防止移除报错
        {
            var rec = targets[i];
            if (rec != null && Vector3.Distance(context.ImpactPoint, rec.transform.position) <= realRadius)
            {
                rec.TakeDamage(finalDmg, context.SourceWeapon.WeaponName + " (溅射)", IsTrueDamage, context.IsCriticalHit);
            }
        }
    }
}