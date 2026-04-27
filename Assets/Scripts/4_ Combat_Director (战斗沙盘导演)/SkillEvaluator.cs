using UnityEngine;

public abstract class SkillEvaluator : ScriptableObject
{
    [Tooltip("该评分项的权重系数，分值 = 计算结果 * Multiplier")]
    public float Multiplier = 1.0f;

    /// <summary>
    /// 计算该技能在此刻的分数
    /// </summary>
    /// <param name="brain">施放者的大脑</param>
    /// <param name="skill">具体技能图纸</param>
    /// <param name="target">当前锁定的目标</param>
    public abstract float CalculateScore(EnemyBrain brain, EnemySkillSO skill, Transform target);
}