using UnityEngine;

[CreateAssetMenu(fileName = "Cond_UniversalCheck", menuName = "Chimera Protocol/Event ECA/万能资源判定")]
public class EventCondition_Universal : EventCondition
{
    public EventResourceType TargetResource;
    public ComparisonType Mode = ComparisonType.GreaterThan;

    public float Threshold = 10f; // 最小值（或单一阈值）
    public float MaxThreshold = 100f; // 最大值（仅在 InRange 模式生效）

    public override bool Evaluate(out string failReason)
    {
        float currentVal = GetValue();
        bool success = false;
        string resName = Translate(TargetResource);

        switch (Mode)
        {
            case ComparisonType.GreaterThan:
                success = currentVal >= Threshold;
                failReason = $"需要 {resName} >= {Threshold} (当前:{currentVal:F0})";
                break;
            case ComparisonType.LessThan:
                success = currentVal <= Threshold;
                failReason = $"需要 {resName} <= {Threshold} (当前:{currentVal:F0})";
                break;
            case ComparisonType.InRange:
                success = currentVal >= Threshold && currentVal <= MaxThreshold;
                failReason = $"需要 {resName} 在 {Threshold}~{MaxThreshold} 之间 (当前:{currentVal:F0})";
                break;
            default: failReason = ""; break;
        }

        if (success) failReason = "";
        return success;
    }

    private float GetValue()
    {
        if (GlobalResourceManager.Instance == null) return 0;
        switch (TargetResource)
        {
            case EventResourceType.CurrentSAN: return GlobalResourceManager.Instance.CurrentSAN;
            case EventResourceType.MaxSAN: return GlobalResourceManager.Instance.MaxSAN;
           
            case EventResourceType.CurrentCP: return GlobalCPManager.Instance?.CurrentCP ?? 0;
            case EventResourceType.MaxCP: return GlobalCPManager.Instance?.GetActualMaxCP() ?? 0;
            case EventResourceType.MaxPowerCapacity: return GlobalResourceManager.Instance.MaxPowerCapacity;
            case EventResourceType.MapDepth: return MapManager.Instance?.CurrentLayer ?? 0;
            default: return 0;
        }
    }

    private string Translate(EventResourceType type)
    {
        switch (type)
        {
            case EventResourceType.CurrentSAN: return "理智度";
 
            case EventResourceType.MaxPowerCapacity: return "电网容量";
            default: return type.ToString();
        }
    }
}