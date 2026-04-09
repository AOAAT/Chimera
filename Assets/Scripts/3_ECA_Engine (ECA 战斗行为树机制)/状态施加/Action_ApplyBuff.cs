using UnityEngine;

[CreateAssetMenu(fileName = "ApplyBuff", menuName = "Chimera Protocol/2. ECA 机制积木/状态 - 施加 Buff (Apply Buff)")]
public class Action_ApplyBuff : ECAAction
{
    [Tooltip("要给目标挂载的 Buff 图纸")]
    public BuffDataSO BuffToApply;

    public override void Execute(ECAContext context)
    {
        if (BuffToApply == null || context.PrimaryTarget == null) return;

        // 尝试从目标身上找到状态管理器
        BuffManager targetBuffMgr = context.PrimaryTarget.GetComponentInParent<BuffManager>();
        if (targetBuffMgr != null)
        {
            targetBuffMgr.ApplyBuff(BuffToApply, context);
        }
    }
}