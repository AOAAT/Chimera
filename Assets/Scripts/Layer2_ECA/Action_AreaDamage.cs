using UnityEngine;

[CreateAssetMenu(fileName = "AreaDamage", menuName = "Chimera/ECA Actions/Area Damage (范围爆炸)")]
public class Action_AreaDamage : ECAAction
{
    [Range(0f, 3f)] public float DamageMultiplier = 1.0f;

    // 爆炸可以自带额外半径，如果填0，就去读武器本身的 ExplosionRadius
    public float BonusRadius = 0f;

    public override void Execute(ECAContext context)
    {
        float baseRadius = context.SourceWeapon.GetStat(StatType.ExplosionRadius);
        float realRadius = (baseRadius + BonusRadius) * CombatSandbox.Instance.DistanceMultiplier;
        float finalDmg = context.BaseDamage * DamageMultiplier;

        DamageReceiver[] all = FindObjectsOfType<DamageReceiver>();
        foreach (var rec in all)
        {
            if (rec.isEnemy && Vector3.Distance(context.ImpactPoint, rec.transform.position) <= realRadius)
            {
                rec.TakeDamage(finalDmg, context.SourceWeapon.WeaponName + " (溅射)");
            }
        }

        // 画紫圈的艺术依然保留！
        DrawDebugCircle(context.ImpactPoint, realRadius, Color.magenta, 0.5f);
    }

    private void DrawDebugCircle(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 36;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
        for (int i = 1; i <= segments; i++)
        {
            angle += 360f / segments * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            Debug.DrawLine(lastPoint, nextPoint, color, duration);
            lastPoint = nextPoint;
        }
    }
}