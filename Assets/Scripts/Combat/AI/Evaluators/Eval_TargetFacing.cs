using UnityEngine;

[CreateAssetMenu(fileName = "Eval_TargetFacing", menuName = "Chimera Protocol/技能评分系统/目标朝向")]
public class Eval_TargetFacing : SkillEvaluator
{
    public enum FacingRequirement { TargetBackwards, TargetForwards }
    public FacingRequirement Requirement = FacingRequirement.TargetBackwards;

    public override float CalculateScore(EnemyBrain brain, EnemySkillSO skill, Transform target)
    {
        if (target == null) return 0;

        // 简易判定：通过 velocity 判断朝向，如果没有 rb 则此评分无效
        Rigidbody2D targetRb = target.GetComponentInParent<Rigidbody2D>();
        if (targetRb == null || targetRb.velocity.sqrMagnitude < 0.1f) return 0;

        Vector2 targetMoveDir = targetRb.velocity.normalized;
        Vector2 toMe = (brain.transform.position - target.position).normalized;

        // 点乘计算
        float dot = Vector2.Dot(targetMoveDir, toMe);

        if (Requirement == FacingRequirement.TargetBackwards)
        {
            // 如果目标移动方向与我到目标的向量一致 (dot > 0)，说明他在背对着我跑
            return Mathf.Max(0, dot * 100f) * Multiplier;
        }
        else
        {
            // 如果点乘为负，说明他在冲着我来
            return Mathf.Max(0, -dot * 100f) * Multiplier;
        }
    }
}