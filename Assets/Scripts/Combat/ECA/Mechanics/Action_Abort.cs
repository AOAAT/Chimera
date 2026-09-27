using UnityEngine;

[CreateAssetMenu(fileName = "Action_Abort", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 终止执行 (Abort)")]
public class Action_Abort : ECAAction
{
    public override void Execute(ECAContext context)
    {
        // 👇【核心指令】：告诉后面的积木，本次开火已经处理完毕，全部熔断
        context.ExecutionAborted = true;

        // Debug.Log("<color=red>【逻辑熔断】</color> 后续 ECA 动作已拦截");
    }
}