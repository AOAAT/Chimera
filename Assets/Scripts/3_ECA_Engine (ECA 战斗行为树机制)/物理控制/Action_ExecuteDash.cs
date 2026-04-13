// --- START OF FILE Action_ExecuteDash.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "ExecuteDash", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 主动冲刺 (Execute Dash)")]
public class Action_ExecuteDash : ECAAction
{
    [Header("=== 冲刺参数 ===")]
    [Tooltip("冲刺速度是基础移速的多少倍？(推荐 3.0 ~ 5.0)")]
    public float SpeedMultiplier = 4.0f;

    [Tooltip("冲刺持续多少秒？(这期间无视正常 AI 走位)")]
    public float Duration = 0.5f;

    [Tooltip("冲刺方向：如果勾选，向着目标冲锋；如果不勾，向着机甲当前面朝的方向冲锋。")]
    public bool DashTowardsTarget = true;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI != null)
        {
            Vector2 dashDir = Vector2.zero;

            // 如果锁定了目标（比如点了技能，或者正在打某个怪）
            if (DashTowardsTarget && context.PrimaryTarget != null)
            {
                dashDir = (context.PrimaryTarget.position - context.SourceEntity.position).normalized;
            }
            else
            {
                // 如果没目标，就顺着机甲当前的速度方向冲过去 (或者干脆随机方向)
                Rigidbody2D rb = context.SourceEntity.GetComponent<Rigidbody2D>();
                if (rb != null && rb.velocity.sqrMagnitude > 0.1f)
                    dashDir = rb.velocity.normalized;
                else
                    dashDir = Random.insideUnitCircle.normalized; // 兜底：乱窜
            }

            myAI.ExecuteDash(dashDir, SpeedMultiplier, Duration);

            // 播放突破音障的爆响 (可选)
            Debug.Log($"<color=#00FFFF>【推进器过载】</color> 机甲启动 {Duration} 秒极速冲刺！速度倍率: {SpeedMultiplier}");
        }
    }
}