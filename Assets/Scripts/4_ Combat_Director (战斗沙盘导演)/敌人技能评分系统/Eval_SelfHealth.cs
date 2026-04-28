using UnityEngine;

[CreateAssetMenu(fileName = "Eval_SelfHealth", menuName = "Chimera Protocol/技能评分系统/个人血量")]
public class Eval_SelfHealth : SkillEvaluator
{
    [Header("=== 触发阈值 ===")]
    [Range(0f, 1f)] public float HPThreshold = 0.3f; // 低于 30% 触发

    [Tooltip("满足阈值时的恐慌分数加成")]
    public float PanicBonus = 100f;

    [Tooltip("是否血量越低分数越高？")]
    public bool ScaleByLoss = true;

    public override float CalculateScore(EnemyBrain brain, EnemySkillSO skill, Transform target)
    {
        DamageReceiver myDR = brain.GetComponent<DamageReceiver>();
        if (myDR == null) return 0;

        float hpPercent = myDR.CurrentHP / myDR.MaxHP;

        if (hpPercent <= HPThreshold)
        {
            if (ScaleByLoss)
            {
                // 血量越少，分越高。
                // 例如 30%血起步，0%血时拿到 1.0 * PanicBonus
                float factor = (HPThreshold - hpPercent) / HPThreshold;
                return factor * PanicBonus * Multiplier;
            }
            return PanicBonus * Multiplier;
        }

        return 0;
    }
}