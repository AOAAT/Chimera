// --- START OF FILE ActiveBuff.cs ---
using UnityEngine;

[System.Serializable]
public class ActiveBuff
{
    public BuffDataSO Blueprint;
    public int CurrentStacks;
    public float RemainingTime;
    private float tickTimer; // 动态计时器

    public ActiveBuff(BuffDataSO data)
    {
        Blueprint = data;
        CurrentStacks = 1;
        RemainingTime = data.BaseDuration;

        // 👇【初始化】：初始倒数设为图纸配好的时间
        tickTimer = data.TickInterval;
    }

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
                // 👇【核心】：未来这里可以接遗物加成！
                // float bonusSpeed = GlobalResourceManager.Instance.DOTSpeedMultiplier;
                // tickTimer = Blueprint.TickInterval * bonusSpeed;

                tickTimer = Blueprint.TickInterval; // 重置为图纸时间

                foreach (var action in Blueprint.OnTickActions)
                    if (action != null) action.Execute(context);
            }
        }
    }
}