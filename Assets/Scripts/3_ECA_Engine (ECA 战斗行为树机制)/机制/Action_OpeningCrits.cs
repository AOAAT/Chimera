using UnityEngine;

[CreateAssetMenu(fileName = "OpeningCrits", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 开场必暴(马夸威特)")]
public class Action_OpeningCrits : ECAAction
{
    [Header("=== 机制配置 ===")]
    [Tooltip("前多少次攻击必定暴击？")]
    public int GuaranteedCount = 3;

    [Header("=== 视觉反馈 ===")]
    public GameObject ShardBreakVFX; // 黑曜石碎裂特效

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        var states = context.SourceWeapon.CustomStates;

        // 1. 初始化计数器
        if (!states.ContainsKey("AttacksMade")) states["AttacksMade"] = 0f;

        float currentCount = states["AttacksMade"];

        // 2. 判定是否在保底次数内
        if (currentCount < GuaranteedCount)
        {
            // 👇【核心操作】：将临时暴击率设为极高，确保必暴
            context.TemporaryCritModifier = 1000f;

            // 3. 视觉表现：黑曜石崩碎
            if (ShardBreakVFX != null)
            {
                SimplePool.Spawn(ShardBreakVFX, context.ImpactPoint, Quaternion.identity);
            }

            Debug.Log($"<color=#666666>【马夸威特】</color> 黑曜石碎裂！第 {currentCount + 1} 次必暴击！");
        }

        // 4. 增加计数
        states["AttacksMade"] = currentCount + 1;
    }
}