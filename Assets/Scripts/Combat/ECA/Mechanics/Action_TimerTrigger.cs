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
            context.CustomStates[timerKey] = 0f;

            // 🌟 重点：在触发下一个积木（发射积木）之前，
            // 必须确保 context 里的 SourceComponentSO 是正确的！
            // 这样发射积木才知道去哪找逻辑代理。
            // (注：由于 OnTick 是在组件循环里被加入列表的，我们通常已经在 context 里带了 source)

            ActionToExecute.Execute(context);
        }
        //Debug.Log($"<color=lime>【心跳】</color> {context.SourceEntity.name} 的计时器进度: {context.CustomStates[timerKey]}");
    }
}