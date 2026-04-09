using UnityEngine;

[CreateAssetMenu(fileName = "ChainsawCritMechanic", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 链锯专属暴击 (Chainsaw Crit)")]
public class Action_ChainsawCritMechanic : ECAAction
{
    [Tooltip("每次未暴击时增加的暴击率 (例如 0.02 代表 2%)")]
    public float CritIncreasePerAttack = 0.02f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        // 【极其优雅的状态机判定】
        if (context.IsCriticalHit)
        {
            // 触发暴击后，暴击率清零！
            context.SourceWeapon.BonusCriticalChance = 0f;
            Debug.LogWarning($"[{context.SourceWeapon.WeaponName}] 释放了暴击！暴击率归零。");
        }
        else
        {
            // 未触发暴击，叠层数！
            context.SourceWeapon.BonusCriticalChance += CritIncreasePerAttack;
            Debug.Log($"[{context.SourceWeapon.WeaponName}] 攻击未暴击。怒气积攒，当前额外暴击率：{context.SourceWeapon.BonusCriticalChance:P}");
        }
    }
}