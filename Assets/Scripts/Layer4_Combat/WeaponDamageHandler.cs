using UnityEngine;

public static class WeaponDamageHandler
{
    public static void DeliverDamage(Vector3 impactPoint, Transform primaryTarget, float damage, RuntimeWeapon data)
    {
        DamageReceiver primaryReceiver = primaryTarget != null ? primaryTarget.GetComponent<DamageReceiver>() : null;

        if (data.TargetType == WeaponTargetType.Single)
        {
            if (primaryReceiver != null) primaryReceiver.TakeDamage(damage, data.WeaponName);
        }
        else if (data.TargetType == WeaponTargetType.AreaOfEffect)
        {
            // 范围爆炸结算 (这里已经完美应用了 DistanceMultiplier)
            float radius = data.GetStat(StatType.ExplosionRadius) * CombatSandbox.Instance.DistanceMultiplier;

            DamageReceiver[] all = Object.FindObjectsOfType<DamageReceiver>();
            foreach (var rec in all)
            {
                if (rec.isEnemy && Vector3.Distance(impactPoint, rec.transform.position) <= radius)
                {
                    rec.TakeDamage(damage, data.WeaponName + " (溅射)");
                }
            }

            // 👇【核心修复】：调用我们自己写的多边形画圆算法！留存 0.5 秒。
            DrawDebugCircle(impactPoint, radius, Color.magenta, 0.5f);
        }
        else if (data.TargetType == WeaponTargetType.MultiTarget)
        {
            // 多目标结算（逻辑保持不变）
            int maxTargets = (int)data.GetStat(StatType.MultiShotCount);
            if (maxTargets <= 0) maxTargets = 3;

            int hitCount = 0;
            DamageReceiver[] all = Object.FindObjectsOfType<DamageReceiver>();

            if (primaryReceiver != null) { primaryReceiver.TakeDamage(damage, data.WeaponName); hitCount++; }

            foreach (var rec in all)
            {
                if (hitCount >= maxTargets) break;
                if (rec.isEnemy && rec != primaryReceiver)
                {
                    float cleaveRange = data.GetStat(StatType.MaxRange) * CombatSandbox.Instance.DistanceMultiplier;
                    if (Vector3.Distance(impactPoint, rec.transform.position) <= cleaveRange)
                    {
                        rec.TakeDamage(damage, data.WeaponName + " (连锁)");
                        hitCount++;
                    }
                }
            }
        }
    }

    // 👇【新增的硬核图形学工具】：用 36 条直线强行画一个圆
    private static void DrawDebugCircle(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 36; // 36 边形，肉眼看起来足够圆了
        float angle = 0f;

        // 计算起始点 (利用三角函数)
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

        for (int i = 1; i <= segments; i++)
        {
            angle += 360f / segments * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

            // 画出这一小段线，并设置留存时间
            Debug.DrawLine(lastPoint, nextPoint, color, duration);
            lastPoint = nextPoint;
        }
    }
}