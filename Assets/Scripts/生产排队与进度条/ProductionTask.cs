using UnityEngine;

[System.Serializable]
public class ProductionTask
{
    public string TaskID;
    public Object SourceSO;      // 记录是哪个底盘或组件
    public string ItemName;      // 冗余记录名字
    public Sprite Icon;          // 冗余记录图标

    public float TotalTime;      // 总需时间
    public float CurrentProgress = 0f; // 当前已完成秒数 (0 到 TotalTime)
    public bool IsPaused = false;      // 是否被玩家点暂停了
    public ResourceSet PaidCost; // 关键：记录此任务支付时的确切金额
    public float NormalizedProgress => Mathf.Clamp01(CurrentProgress / TotalTime);
    public float RemainingTime => Mathf.Max(0, TotalTime - CurrentProgress);

    public ProductionTask(UnityEngine.Object so, string name, Sprite icon, float time, ResourceSet cost)
    {
        TaskID = System.Guid.NewGuid().ToString();
        SourceSO = so;
        ItemName = name;
        Icon = icon;
        TotalTime = time;
        PaidCost = cost; // 存入成本
    }
}