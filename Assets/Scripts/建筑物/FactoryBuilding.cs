using System.Collections.Generic;
using UnityEngine;

public class FactoryBuilding : BuildingBase
{
    public bool SyncOrderFlag = false; // 用于通知 UI：顺序已变，不需要销毁重建，只需保持现状

    [Header("=== 生产任务队列 ===")]
    public List<ProductionTask> TaskQueue = new List<ProductionTask>();
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
        // 1. 计算当前可用的并行产线总数
        // 0~1人 = 1条线, 2~3人 = 2条线, 4人 = 3条线...
        int totalSlots = 1 + (currentStaff.Count / 2);

        // 2. 追踪本帧已占用的产线数
        int slotsInUse = 0;

        // 3. 遍历任务队列（使用 for 循环以便处理完成后的移除操作）
        for (int i = 0; i < TaskQueue.Count; i++)
        {
            // 如果所有产线都已占满，停止遍历后续任务
            if (slotsInUse >= totalSlots) break;

            ProductionTask task = TaskQueue[i];

            // 跳过被玩家手动暂停的任务，且不占用产线名额
            if (task.IsPaused) continue;

            // --- 执行生产步进 ---
            task.CurrentProgress += deltaTime;
            slotsInUse++; // 占用一个产线名额

            // 检查任务是否完成
            if (task.CurrentProgress >= task.TotalTime)
            {
                FinishTask(task);

                // 🌟 重要：因为 FinishTask 会从 TaskQueue 中 Remove 掉任务，
                // 我们需要将索引回退，并减少已用名额，让下一个任务在同一帧补位
                i--;
                slotsInUse--;
            }
        }
    }


    private void FinishTask(ProductionTask task)
    {
        // 1. 实物入库
        if (task.SourceSO is ChassisDataSO chassis)
            PlayerInventoryManager.Instance.AddChassisToWarehouse(chassis, 1);
        else if (task.SourceSO is ComponentDataSO component)
            PlayerInventoryManager.Instance.AddComponentToWarehouse(component, 1, 1);

        // 2. 从队列移除
        TaskQueue.Remove(task);
        Debug.Log($"<color=green>【生产完成】</color> {task.ItemName} 已产出并出库。");
        GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ItemEject);
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
            // 这里未来可以触发 UI 抖动提示
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
}