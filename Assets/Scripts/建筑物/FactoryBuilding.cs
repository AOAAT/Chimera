using System.Collections.Generic;
using UnityEngine;

public class FactoryBuilding : BuildingBase
{
    public bool SyncOrderFlag = false; // 用于通知 UI：顺序已变，不需要销毁重建，只需保持现状

    [Header("=== 生产任务队列 ===")]
    public List<ProductionTask> TaskQueue = new List<ProductionTask>();

    [Header("=== 居民生产力 ===")]
    [Min(0f)] public float SpeedBonusPerProductivity = 0.15f;
    [Min(0.1f)] public float ProductivityPerAdditionalLine = 2f;
    [Min(1)] public int MaxProductionLines = 3;
    public ResidentWorkDomain WorkDomain = ResidentWorkDomain.Tech;

    public float TotalStaffProductivity
    {
        get
        {
            float total = 0f;
            ResidentIdentityLibrarySO library = PopulationManager.Instance != null
                ? PopulationManager.Instance.IdentityLibrary : null;
            foreach (ResidentData resident in currentStaff)
                total += ResidentWorkCalculator.CalculateContribution(resident, WorkDomain, library);
            return total;
        }
    }

    public float ProductionSpeedMultiplier => 1f + TotalStaffProductivity * SpeedBonusPerProductivity;
    public int ActiveProductionLineCount => Mathf.Clamp(
        1 + Mathf.FloorToInt(TotalStaffProductivity / Mathf.Max(0.1f, ProductivityPerAdditionalLine)),
        1, Mathf.Max(1, MaxProductionLines));
    public bool HasMaximumProductionLines => ActiveProductionLineCount >= Mathf.Max(1, MaxProductionLines);
    public float NextProductionLineThreshold => HasMaximumProductionLines
        ? TotalStaffProductivity
        : ActiveProductionLineCount * Mathf.Max(0.1f, ProductivityPerAdditionalLine);
    public float ProductivityUntilNextLine => HasMaximumProductionLines
        ? 0f
        : Mathf.Max(0f, NextProductionLineThreshold - TotalStaffProductivity);
    public float NextProductionLineProgress
    {
        get
        {
            if (HasMaximumProductionLines) return 1f;
            float step = Mathf.Max(0.1f, ProductivityPerAdditionalLine);
            float previousThreshold = (ActiveProductionLineCount - 1) * step;
            return Mathf.InverseLerp(previousThreshold, previousThreshold + step, TotalStaffProductivity);
        }
    }

    public float GetEffectiveProductionTime(float baseTime)
    {
        return Mathf.Max(0f, baseTime) / Mathf.Max(0.01f, ProductionSpeedMultiplier);
    }

    public float GetResidentContribution(ResidentData resident)
    {
        ResidentIdentityLibrarySO library = PopulationManager.Instance != null
            ? PopulationManager.Instance.IdentityLibrary : null;
        return ResidentWorkCalculator.CalculateContribution(resident, WorkDomain, library);
    }
    protected override void Awake()
    {
        base.Awake();
        // 如果你是直接拖在场景里的测试建筑，强制设为 true
        // 如果是建造出来的，它会被 BuildingManager 设为 true
        if (transform.parent == null) isPlaced = true;
    }
    private void Update()
    {
        // 只有被放置在世界上且队列有东西时才开工
        if (!isPlaced || TaskQueue.Count == 0) return;

        UpdateProduction(Time.deltaTime);
    }

    private void UpdateProduction(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        float speed = ProductionSpeedMultiplier;
        foreach (ProductionTask task in TaskQueue)
        {
            task.IsActivelyProducing = false;
            task.ActiveLineIndex = -1;
            task.EffectiveSpeed = speed;
        }

        List<ProductionTask> activeTasks = new List<ProductionTask>();
        int availableLines = ActiveProductionLineCount;
        for (int i = 0; i < TaskQueue.Count && activeTasks.Count < availableLines; i++)
        {
            ProductionTask task = TaskQueue[i];
            if (!task.IsPaused)
            {
                task.IsActivelyProducing = true;
                task.ActiveLineIndex = activeTasks.Count;
                activeTasks.Add(task);
            }
        }

        foreach (ProductionTask task in activeTasks)
        {
            EnsureCraftSnapshot(task);
            task.EffectiveSpeed = speed;
            task.CurrentProgress += deltaTime * speed;
        }

        for (int i = activeTasks.Count - 1; i >= 0; i--)
            if (activeTasks[i].CurrentProgress >= activeTasks[i].TotalTime)
                FinishTask(activeTasks[i]);
    }

    private void FinishTask(ProductionTask task)
    {
        // 1. 实物入库
        if (task.SourceSO is ChassisDataSO chassis)
            PlayerInventoryManager.Instance.AddChassisToWarehouse(chassis, 1);
        else if (task.SourceSO is ComponentDataSO component)
        {
            EnsureCraftSnapshot(task);
            InstancedComponent product = ComponentQualityGenerator.Create(component, 1, task.CraftSeed,
                task.Craftsmanship, task.CraftedByResidentIDs, task.CraftedAtBuildingID);
            PlayerInventoryManager.Instance.AddComponentInstance(product);
            string affixes = product.Affixes.Count > 0
                ? $" · {string.Join("、", product.Affixes.ConvertAll(affix => affix.DisplayName))}"
                : string.Empty;
            UIFeedback.Show($"{ComponentQualityUtility.GetName(product.Quality)} {component.ComponentName} 已入库{affixes}");
        }

        // 2. 从队列移除
        TaskQueue.Remove(task);
        Debug.Log($"<color=green>【生产完成】</color> {task.ItemName} 已产出并出库。");
        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Loot_ItemEject);
    }

    private void EnsureCraftSnapshot(ProductionTask task)
    {
        if (task == null || task.HasCraftSnapshot) return;
        task.HasCraftSnapshot = true;
        task.CraftSeed = System.Guid.NewGuid().GetHashCode();
        task.Craftsmanship = TotalStaffProductivity;
        task.CraftedAtBuildingID = PersistentID;
        task.CraftedByResidentIDs = currentStaff.FindAll(resident => resident != null)
            .ConvertAll(resident => resident.InstanceID);
    }

    // --- 给 UI 调用：添加新任务 ---
    public void AddToQueue(UnityEngine.Object so, string n, Sprite icon, float time, ResourceSet cost)
    {
        // 1. 资源契约校验
        if (GlobalResourceManager.Instance.TryConsume(cost))
        {
            TaskQueue.Add(new ProductionTask(so, n, icon, time, cost));
            Debug.Log($"<color=cyan>【支付成功】</color> 消耗了 {cost.Scrap} 废料，开始生产 {n}");
        }
        else
        {
            Debug.LogWarning("【系统】 资源储备不足，无法开始生产任务。");
            UIFeedback.Show(UIFeedback.Shortage(cost));
        }
    }

    public void CancelTask(ProductionTask task)
    {
        if (TaskQueue.Contains(task))
        {
            // 2. 全额返还契约
            GlobalResourceManager.Instance.Refund(task.PaidCost);
            TaskQueue.Remove(task);
            Debug.Log($"<color=orange>【任务撤回】</color> 已全额返还：{task.ItemName}");
        }
    }

    public List<ProductionTaskSaveData> CaptureProductionQueue()
    {
        List<ProductionTaskSaveData> result = new List<ProductionTaskSaveData>();
        foreach (ProductionTask task in TaskQueue)
        {
            string type = null;
            string definitionID = null;
            if (task.SourceSO is ChassisDataSO chassis)
            {
                type = "Chassis";
                definitionID = chassis.ChassisID;
            }
            else if (task.SourceSO is ComponentDataSO component)
            {
                type = "Component";
                definitionID = component.ComponentBaseID;
            }
            if (definitionID == null) continue;
            result.Add(new ProductionTaskSaveData
            {
                TaskID = task.TaskID,
                DefinitionType = type,
                DefinitionID = definitionID,
                ItemName = task.ItemName,
                TotalTime = task.TotalTime,
                CurrentProgress = task.CurrentProgress,
                IsPaused = task.IsPaused,
                PaidCost = task.PaidCost,
                HasCraftSnapshot = task.HasCraftSnapshot,
                CraftSeed = task.CraftSeed,
                Craftsmanship = task.Craftsmanship,
                CraftedAtBuildingID = task.CraftedAtBuildingID,
                CraftedByResidentIDs = new List<string>(task.CraftedByResidentIDs ?? new List<string>())
            });
        }
        return result;
    }

    public void RestoreProductionQueue(List<ProductionTaskSaveData> savedQueue, SaveDefinitionResolver definitions)
    {
        TaskQueue.Clear();
        if (savedQueue == null) return;
        foreach (ProductionTaskSaveData saved in savedQueue)
        {
            UnityEngine.Object definition;
            Sprite icon;
            if (saved.DefinitionType == "Chassis")
            {
                ChassisDataSO chassis = definitions.ResolveChassis(saved.DefinitionID);
                definition = chassis;
                icon = chassis != null ? chassis.ChassisSprite : null;
            }
            else
            {
                ComponentDataSO component = definitions.ResolveComponent(saved.DefinitionID);
                definition = component;
                icon = component != null ? component.ComponentIcon : null;
            }
            if (definition == null) continue;
            TaskQueue.Add(ProductionTask.Restore(definition, saved.ItemName, icon, saved.TotalTime,
                saved.PaidCost, saved.TaskID, saved.CurrentProgress, saved.IsPaused,
                saved.HasCraftSnapshot, saved.CraftSeed, saved.Craftsmanship,
                saved.CraftedAtBuildingID, saved.CraftedByResidentIDs));
        }
    }
}
