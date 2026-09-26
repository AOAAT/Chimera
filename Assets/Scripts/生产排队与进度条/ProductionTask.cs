using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ProductionTask
{
    public string TaskID;
    public Object SourceSO;
    public string ItemName;
    public Sprite Icon;

    public float TotalTime;
    public float CurrentProgress;
    public bool IsPaused;
    public ResourceSet PaidCost;

    public bool HasCraftSnapshot;
    public int CraftSeed;
    public float Craftsmanship;
    public string CraftedAtBuildingID;
    public List<string> CraftedByResidentIDs = new List<string>();

    [System.NonSerialized] public bool IsActivelyProducing;
    [System.NonSerialized] public int ActiveLineIndex = -1;
    [System.NonSerialized] public float EffectiveSpeed = 1f;

    public float NormalizedProgress => TotalTime <= 0f ? 1f : Mathf.Clamp01(CurrentProgress / TotalTime);
    public float RemainingTime => Mathf.Max(0f, TotalTime - CurrentProgress);

    public ProductionTask(Object source, string name, Sprite icon, float time, ResourceSet cost)
    {
        TaskID = System.Guid.NewGuid().ToString();
        SourceSO = source;
        ItemName = name;
        Icon = icon;
        TotalTime = time;
        PaidCost = cost;
    }

    public static ProductionTask Restore(Object source, string name, Sprite icon, float time,
        ResourceSet cost, string taskID, float progress, bool paused, bool hasCraftSnapshot = false,
        int craftSeed = 0, float craftsmanship = 0f, string craftedAtBuildingID = null,
        List<string> craftedByResidentIDs = null)
    {
        ProductionTask task = new ProductionTask(source, name, icon, time, cost)
        {
            TaskID = taskID,
            CurrentProgress = Mathf.Clamp(progress, 0f, time),
            IsPaused = paused,
            HasCraftSnapshot = hasCraftSnapshot,
            CraftSeed = craftSeed,
            Craftsmanship = craftsmanship,
            CraftedAtBuildingID = craftedAtBuildingID ?? string.Empty,
            CraftedByResidentIDs = craftedByResidentIDs != null
                ? new List<string>(craftedByResidentIDs) : new List<string>()
        };
        return task;
    }
}
