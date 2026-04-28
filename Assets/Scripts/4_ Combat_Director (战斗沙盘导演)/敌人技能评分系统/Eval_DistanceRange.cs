using UnityEngine;

[CreateAssetMenu(fileName = "Eval_Distance", menuName = "Chimera Protocol/技能评分系统/距离范围")]
public class Eval_DistanceRange : SkillEvaluator
{
    public enum DistancePreference { PreferClose, PreferFar, WithinIdealRange }

    [Header("=== 偏好设置 ===")]
    public DistancePreference Preference = DistancePreference.PreferClose;
    public float IdealDistance = 5f;

    public override float CalculateScore(EnemyBrain brain, EnemySkillSO skill, Transform target)
    {
        if (target == null) return 0;

        float dist = Vector2.Distance(brain.transform.position, target.position);
        float score = 0;

        switch (Preference)
        {
            case DistancePreference.PreferClose:
                // 越近分越高，10距离为0分，0距离为10分
                score = Mathf.Max(0, 10f - dist);
                break;

            case DistancePreference.PreferFar:
                // 越远分越高
                score = dist;
                break;

            case DistancePreference.WithinIdealRange:
                // 在理想距离附近分最高 (使用高斯分布或简单的反比)
                float diff = Mathf.Abs(dist - IdealDistance);
                score = Mathf.Max(0, 10f - diff);
                break;
        }

        return score * Multiplier;
    }
}