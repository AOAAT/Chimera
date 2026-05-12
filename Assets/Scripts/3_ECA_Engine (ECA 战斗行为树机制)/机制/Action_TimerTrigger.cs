// --- Action_TimerTrigger.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "TimerTrigger_V2", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 周期计时器 V2")]
public class Action_TimerTrigger : ECAAction
{
    public float Interval = 5.0f;
    public ECAAction ActionToExecute;

    // 属于逻辑闸门层
    public Action_TimerTrigger() { Priority = 50; }

    public override void Execute(ECAContext context)
    {
        if (ActionToExecute == null) return;

        // 使用积木资源的 InstanceID 作为独立计时的 Key
        // 这样即使同一台机甲装了两个同样的积木，也不会相互干扰
        string timerKey = "Timer_" + this.GetInstanceID();

        if (!context.CustomStates.ContainsKey(timerKey))
            context.CustomStates[timerKey] = 0f;

        context.CustomStates[timerKey] += Time.deltaTime;

        if (context.CustomStates[timerKey] >= Interval)
        {
            context.CustomStates[timerKey] = 0f; // 重置

            // 🚀 触发真正的行为 (如：大象腿冲撞、肾上腺素打针)
            ActionToExecute.Execute(context);
        }
    }
}