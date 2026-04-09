using UnityEngine;

[CreateAssetMenu(fileName = "ApplyImpulse", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 动能击退 (Apply Impulse)")]
public class Action_ApplyImpulse : ECAAction
{
    [Header("=== 物理打击参数 ===")]
    [Tooltip("基础冲量大小 (将被目标的质量稀释)")]
    public float BaseImpulse = 200f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        // 1. 计算受力方向 (由爆炸中心/子弹位置 指向 目标中心)
        Vector2 forceDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;

        // 兜底：如果正好在同一坐标，给个随机方向
        if (forceDir == Vector2.zero) forceDir = Random.insideUnitCircle.normalized;

        // 2. 尝试寻找敌人大脑并施加物理打击
        EnemyBrain enemy = context.PrimaryTarget.GetComponentInParent<EnemyBrain>();
        if (enemy != null)
        {
            enemy.ApplyImpulse(forceDir, BaseImpulse);
            return;
        }

        // 3. 尝试寻找玩家大脑并施加物理打击
        ChimeraAIController player = context.PrimaryTarget.GetComponentInParent<ChimeraAIController>();
        if (player != null)
        {
            player.ApplyImpulse(forceDir, BaseImpulse);
            return;
        }
    }
}