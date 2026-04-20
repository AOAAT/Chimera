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

        // 1. 计算受力方向
        Vector2 forceDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        if (forceDir == Vector2.zero) forceDir = Random.insideUnitCircle.normalized;

        // 【核心修复】：积木冲量必须乘以 SpeedMultiplier
        // 因为 $v = at$，当我们缩放了速度（SpeedMultiplier），为了产生相同的位移占比，冲量也必须同步。
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float scaledImpulse = BaseImpulse * speedMult;

        // 2. 尝试寻找大脑并施加打击
        EnemyBrain enemy = context.PrimaryTarget.GetComponentInParent<EnemyBrain>();
        if (enemy != null)
        {
            enemy.ApplyImpulse(forceDir, scaledImpulse);
            return;
        }

        ChimeraAIController player = context.PrimaryTarget.GetComponentInParent<ChimeraAIController>();
        if (player != null)
        {
            player.ApplyImpulse(forceDir, scaledImpulse);
            return;
        }
    }
}