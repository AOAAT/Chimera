using UnityEngine;

[CreateAssetMenu(fileName = "TimerTrigger", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 周期计时器")]
public class Action_TimerTrigger : ECAAction
{
    public float Interval = 5f;
    public ECAAction ActionToExecute;

    public override void Execute(ECAContext context)
    {
        // 我们利用 SourceWeapon 的 CustomStates 来存储每个组件独立的计时
        if (context.SourceWeapon == null) return;

        string timerKey = "InternalTimer_" + this.name;
        if (!context.SourceWeapon.CustomStates.ContainsKey(timerKey))
            context.SourceWeapon.CustomStates[timerKey] = Time.time;

        if (Time.time >= context.SourceWeapon.CustomStates[timerKey] + Interval)
        {
            ActionToExecute?.Execute(context);
            context.SourceWeapon.CustomStates[timerKey] = Time.time;
            Debug.Log($"【周期触发】执行了：{ActionToExecute.name}");
        }
    }
}