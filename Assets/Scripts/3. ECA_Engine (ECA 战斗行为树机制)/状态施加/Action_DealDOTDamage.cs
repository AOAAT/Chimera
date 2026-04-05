// --- START OF FILE Action_DealDOTDamage.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "DealDOTDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成持续伤害 (DOT Damage)")]
public class Action_DealDOTDamage : ECAAction
{
    [Header("=== DOT 伤害设定 ===")]
    [Tooltip("每 1 层 Buff 造成的伤害值")]
    public float DamagePerStack = 5f;

    [Tooltip("是否为真实伤害 (无视护甲)？毒液通常是真实伤害")]
    public bool IsTrueDamage = true;

    [Tooltip("如果该积木是由 Buff 触发的，需要填入对应的 Buff 蓝图以读取层数")]
    public BuffDataSO SourceBuff;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null || SourceBuff == null) return;

        // 1. 尝试从目标身上找到状态管理器
        BuffManager targetBuffMgr = context.PrimaryTarget.GetComponentInParent<BuffManager>();
        if (targetBuffMgr == null) return;

        // 2. 找到指定的 Buff 并读取层数
        int currentStacks = targetBuffMgr.GetBuffStacks(SourceBuff.BuffID);
        if (currentStacks <= 0) return;

        // 3. 找到目标的血条
        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            // 4. 结算：总伤害 = 基础伤害 * 层数
            float finalDamage = DamagePerStack * currentStacks;

            // 这里可以预留未来“全局遗物加成”的接口 (例如：GlobalResourceManager.BonusAcidDamage)
            // finalDamage += GlobalResourceManager.Instance.GetBonusDOTDamage(SourceBuff.BuffID);

            // 发送伤害 (如果是毒液，这里会飘出你之前配好的紫色真伤数字！)
            receiver.TakeDamage(finalDamage, SourceBuff.BuffName, IsTrueDamage, false);
        }
    }
}