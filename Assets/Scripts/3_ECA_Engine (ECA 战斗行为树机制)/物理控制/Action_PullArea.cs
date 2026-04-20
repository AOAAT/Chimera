using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "PullArea", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 引力坍缩 (Pull)")]
public class Action_PullArea : ECAAction
{
    [Header("=== 引力配置 ===")]
    public float PullRadius = 8f;
    [Tooltip("吸附力度。建议 600-1200")]
    public float PullForce = 800f;

    public override void Execute(ECAContext context)
    {
        // 1. 确定黑洞中心（子弹落点）
        float realRadius = CombatSandbox.GetDist(PullRadius);
        Vector3 center = context.ImpactPoint;

        // 2. 寻找受害者
        var allPotentials = CombatDirector.ActiveEnemies.Concat(CombatDirector.ActivePlayerUnits);

        var targets = allPotentials
            .Where(r => r != null && r.CurrentHP > 0)
            .Where(r => Vector3.Distance(center, r.transform.position) <= realRadius);

        foreach (var t in targets)
        {
            // 不拉扯发射者自己，防止把自己吸飞
            if (t.transform == context.SourceEntity) continue;

            // 👇【数学修正】：强制计算指向中心的向量
            // 方向 = 中心点 - 敌人当前点
            Vector2 pullDir = (Vector2)(center - t.transform.position);
            float distance = pullDir.magnitude;

            // 如果已经非常接近中心了，就不再加力，防止在中心点鬼畜抖动
            if (distance < 0.2f) continue;

            // 归一化方向
            pullDir.Normalize();

            // 尝试呼叫物理引擎
            EnemyBrain enemy = t.GetComponent<EnemyBrain>();
            if (enemy != null)
            {
                // 传入向心力
                enemy.ApplyImpulse(pullDir, PullForce);
            }

            ChimeraAIController player = t.GetComponent<ChimeraAIController>();
            if (player != null)
            {
                player.ApplyImpulse(pullDir, PullForce);
            }
        }

        // 视觉调试：紫色的十字星代表黑洞中心
        Debug.DrawLine(center + Vector3.up, center + Vector3.down, Color.magenta, 0.5f);
        Debug.DrawLine(center + Vector3.left, center + Vector3.right, Color.magenta, 0.5f);
    }
}