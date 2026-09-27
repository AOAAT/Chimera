using System.Collections.Generic;
using UnityEngine;

// 1. 将枚举定义移到这里
public enum TargetCondition { TargetHasNoArmor, TargetBelowHPPercent, Always }

[CreateAssetMenu(fileName = "ConditionGate", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 条件栅栏")]
public class Action_ConditionGate : ECAAction
{
    public enum ConditionRequirement { AllMet, AnyMet }

    [Header("=== 熔断规则 ===")]
    public ConditionRequirement Requirement = ConditionRequirement.AllMet;

    // 2. 使用本地定义的枚举
    public List<TargetCondition> Conditions;

    [Header("=== 满足时执行的动作 ===")]
    public List<ECAAction> SubActions;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;
        DamageReceiver dr = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (dr == null) return;

        bool isAllMet = true;
        bool isAnyMet = false;

        foreach (var cond in Conditions)
        {
            bool met = false;
            if (cond == TargetCondition.TargetHasNoArmor) met = (dr.CurrentAP <= 0);
            else if (cond == TargetCondition.TargetBelowHPPercent) met = (dr.CurrentHP / dr.MaxHP <= 0.5f);
            else if (cond == TargetCondition.Always) met = true;

            if (met) isAnyMet = true;
            else isAllMet = false;
        }

        bool finalPass = (Requirement == ConditionRequirement.AllMet) ? isAllMet : isAnyMet;

        if (finalPass && SubActions != null)
        {
            foreach (var a in SubActions)
            {
                if (a != null) a.Execute(context);
            }
        }
    }
}