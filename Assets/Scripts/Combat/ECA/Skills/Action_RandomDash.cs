using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "RandomDash", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 随机冲撞(马头专用)")]
public class Action_RandomDash : ECAAction
{
    [Header("=== 冲撞配置 ===")]
    public float SpeedMultiplier = 8.0f; // 建议调高点，更有冲击感
    public float Duration = 0.5f;

    [Tooltip("向前冲撞的概率")]
    [Range(0f, 1f)] public float AggressiveChance = 0.5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI == null) return;

        // 1. 获取参考方向的目标
        Transform realTarget = null;

        // 如果 Context 里的目标是自己，说明是主动技能触发，需要现找一个敌人
        if (context.PrimaryTarget == context.SourceEntity)
        {
            // 利用我们优化过的静态列表，寻找最近的敌人
            var nearestEnemy = CombatDirector.ActiveEnemies
                .Where(e => e != null && e.CurrentHP > 0)
                .OrderBy(e => Vector3.Distance(context.SourceEntity.position, e.transform.position))
                .FirstOrDefault();

            if (nearestEnemy != null) realTarget = nearestEnemy.transform;
        }
        else
        {
            realTarget = context.PrimaryTarget;
        }

        // 2. 计算最终冲刺向量
        Vector2 dashDir = Vector2.zero;

        if (realTarget != null)
        {
            bool isAggressive = Random.value <= AggressiveChance;
            Vector2 toTarget = (realTarget.position - context.SourceEntity.position).normalized;

            dashDir = isAggressive ? toTarget : -toTarget;

            string mode = isAggressive ? "向前猛冲" : "战术后撤";
            Debug.Log($"<color=#FF4500>【马头核心】</color> 锁定了目标 {realTarget.name}，执行了 {mode}！");
        }
        else
        {
            // 实在没目标，就随机乱冲
            dashDir = Random.insideUnitCircle.normalized;
            Debug.Log("<color=#FF4500>【马头核心】</color> 没找到目标，开始发疯乱冲！");
        }

        // 3. 👇【核心加固】：确保向量不是零
        if (dashDir == Vector2.zero) dashDir = Vector2.up;

        // 4. 执行物理冲刺
        myAI.ExecuteDash(dashDir, SpeedMultiplier, Duration);
    }
}