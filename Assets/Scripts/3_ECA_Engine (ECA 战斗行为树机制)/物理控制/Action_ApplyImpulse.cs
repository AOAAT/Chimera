using UnityEngine;

[CreateAssetMenu(fileName = "ApplyImpulse", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 动能击退 (Push)")]
public class Action_ApplyImpulse : ECAAction
{
    [Header("=== 物理打击参数 ===")]
    [Tooltip("基础冲量大小 (将被目标的质量稀释)")]
    public float BaseImpulse = 200f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        // 👇【推力逻辑】：目标位置 - 来源位置 = 向外弹开
        Vector2 pushDir = (Vector2)(context.PrimaryTarget.position - context.ImpactPoint).normalized;

        // 兜底：如果正好在同一坐标，给个随机方向
        if (pushDir == Vector2.zero) pushDir = Random.insideUnitCircle.normalized;

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float scaledImpulse = BaseImpulse * speedMult;

        EnemyBrain enemy = context.PrimaryTarget.GetComponentInParent<EnemyBrain>();
        if (enemy != null) enemy.ApplyImpulse(pushDir, scaledImpulse);

        ChimeraAIController player = context.PrimaryTarget.GetComponentInParent<ChimeraAIController>();
        if (player != null) player.ApplyImpulse(pushDir, scaledImpulse);
    }
}