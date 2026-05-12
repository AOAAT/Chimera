// --- Action_TimerTrigger.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "TimerTrigger", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 周期计时器")]
public class Action_TimerTrigger : ECAAction
{
    public float Interval = 5.0f;
    public ECAAction ActionToExecute; // 时间到了要执行哪个动作？

    public Action_TimerTrigger() { Priority = 50; } // 属于闸门层

    public override void Execute(ECAContext context)
    {
        if (ActionToExecute == null) return;

        // 利用 context 中的字典，为每个零件实例维护独立的计时器
        // Key 规则：零件名 + 积木名
        string timerKey = this.name + "_Timer";

        if (!context.CustomStates.ContainsKey(timerKey))
            context.CustomStates[timerKey] = 0f;

        // 累计时间
        context.CustomStates[timerKey] += Time.deltaTime;

        // 判定是否到期
        if (context.CustomStates[timerKey] >= Interval)
        {
            // 重置计时
            context.CustomStates[timerKey] = 0f;

            // 🚀 执行目标动作（如：大象腿冲撞）
            ActionToExecute.Execute(context);
        }
    }
}