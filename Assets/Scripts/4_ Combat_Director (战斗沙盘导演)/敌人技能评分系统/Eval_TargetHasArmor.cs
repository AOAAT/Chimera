using UnityEngine;

[CreateAssetMenu(fileName = "Eval_TargetHasArmor", menuName = "Chimera Protocol/技能评分系统/是否有护甲")]
public class Eval_TargetHasArmor : SkillEvaluator
{
    public float BonusScore = 50f;

    public override float CalculateScore(EnemyBrain brain, EnemySkillSO skill, Transform target)
    {
        DamageReceiver dr = target.GetComponentInParent<DamageReceiver>();
        if (dr != null && dr.CurrentAP > 0)
        {
            return BonusScore * Multiplier;
        }
        return 0;
    }
}