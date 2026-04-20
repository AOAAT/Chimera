using UnityEngine;
using System.Linq; // 必须加这个

[CreateAssetMenu(fileName = "TriggerBuffTicks", menuName = "Chimera Protocol/2. ECA 机制积木/状态 - 强制状态结算")]
public class Action_TriggerBuffTicks : ECAAction
{
    public BuffDataSO TargetBuff;
    public int TickCount = 3;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null || TargetBuff == null) return;

        BuffManager mgr = context.PrimaryTarget.GetComponentInParent<BuffManager>();
        if (mgr != null)
        {
            // 👇 使用 Linq 的 FirstOrDefault 代替 Find
            var activeBuff = mgr.GetActiveBuffs().FirstOrDefault(b => b.Blueprint.BuffID == TargetBuff.BuffID);

            if (activeBuff != null)
            {
                for (int i = 0; i < TickCount; i++)
                {
                    foreach (var action in TargetBuff.OnTickActions)
                    {
                        action.Execute(context);
                    }
                }
            }
        }
    }
}