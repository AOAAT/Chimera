using UnityEngine;

[System.Serializable]
public class ActiveBuff
{
    public BuffDataSO Blueprint;
    public int CurrentStacks;
    public float RemainingTime;
    private float tickTimer = 1f; // 记录 OnTick 触发的 1 秒间隔

    public ActiveBuff(BuffDataSO data)
    {
        Blueprint = data;
        CurrentStacks = 1;
        RemainingTime = data.BaseDuration;
    }

    // 处理每帧倒计时与 Tick 触发
    public void UpdateTimers(ECAContext context)
    {
        if (Blueprint.DurationType != BuffDurationType.Permanent)
        {
            RemainingTime -= Time.deltaTime;
        }

        if (Blueprint.OnTickActions.Count > 0)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = 1f; // 重置 1 秒
                // 每次 Tick 触发时，可以把当前层数作为一个乘区传给 Action (未来扩展)
                foreach (var action in Blueprint.OnTickActions) if (action != null) action.Execute(context);
            }
        }
    }
}