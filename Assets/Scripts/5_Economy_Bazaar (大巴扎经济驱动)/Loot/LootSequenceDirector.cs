using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LootSequenceDirector : MonoBehaviour
{
    public static LootSequenceDirector Instance;

    private MacroCategory currentMacroContext;
    private int currentDepthContext;

    private void Awake() { if (Instance == null) Instance = this; }

    // ==========================================
    // 全新入口：双源合流与集散中心启动
    // ==========================================
    public void StartLootHub(LootSequenceSO encounterLoot, LootSequenceSO nodeLoot, MacroCategory macro, int mapDepth, System.Action onComplete = null)
    {
        currentMacroContext = macro;
        currentDepthContext = mapDepth;

        Debug.Log("<color=#FFD700>【打捞管线启动】</color> 正在合并双源战利品并重排优先级...");

        // 1. 熔炉合并 (将微观遭遇战和宏观节点补偿合并)
        List<LootTaskConfig> combinedConfigs = new List<LootTaskConfig>();
        if (encounterLoot != null) combinedConfigs.AddRange(encounterLoot.Tasks);
        if (nodeLoot != null) combinedConfigs.AddRange(nodeLoot.Tasks);

        // 2. 同类排序 (优先级：保底单抽 -> 盲盒 -> 自选标签 -> 定制极品)
        // 巧妙利用 Enum 的底层 int 值 (0,1,2,3) 进行自然排序！
        combinedConfigs = combinedConfigs.OrderBy(t => (int)t.Mode).ToList();

        // 3. 包装成运行时状态机
        List<ActiveLootTask> activeTasks = combinedConfigs.Select(t => new ActiveLootTask { Config = t }).ToList();

        if (activeTasks.Count == 0)
        {
            Debug.LogWarning("【打捞警告】双源掉落池均为空，直接返回地图！");
            CombatDirector.Instance.ExecuteReturnToMap();
            return;
        }

        // 4. 呼叫 UI 展开集散大厅！
        LootUIManager.Instance.OpenHub(activeTasks, onComplete);
    }

    // ==========================================
    // 核心算法：为指定的任务“开盲盒” (锁死数据)
    // ==========================================
    public void RollItemsForTask(ActiveLootTask task)
    {
        if (task.IsBoxOpened) return; // 已经开过光了，绝对不能再 Roll！

        List<InstancedComponent> loot = new List<InstancedComponent>();

        switch (task.Config.Mode)
        {
            case LootDropMode.MacroCategorySingle:
                var macros1 = GetTargetMacrosForTask(task.Config);
                // 👇【修复点】：传入 task.Config 供底层读取权重
                loot = GenerateLoot(c => macros1.Contains(c.MacroCategory), task.Config, currentDepthContext);
                break;

            case LootDropMode.SystemAssignedTag:
                var pool2 = GetTagPoolForTask(task.Config);
                if (pool2.Count > 0)
                {
                    SubTag sysTag = pool2.OrderBy(x => Guid.NewGuid()).First();
                    // 👇【修复点】：传入 task.Config
                    loot = GenerateLoot(c => c.BaseSubTags.Contains(sysTag), task.Config, currentDepthContext);
                }
                break;

            case LootDropMode.PlayerDrivenFilter:
                // 注意：走到这里时，玩家一定已经选好了标签，并存在了 LockedTag 里！
                if (task.LockedTag.HasValue)
                {
                    // 👇【修复点】：传入 task.Config
                    loot = GenerateLoot(c => c.BaseSubTags.Contains(task.LockedTag.Value), task.Config, currentDepthContext);
                }
                break;

            case LootDropMode.CustomPoolDrop:
                loot = GenerateCustomLoot(task.Config.CustomPool, task.Config.TripleChoiceProbability);
                break;
        }

        // 锁死数据！
        task.GeneratedItems = loot;
        task.IsBoxOpened = true;
        Debug.Log($"【盲盒开启】生成了 {loot.Count} 个装备，数据已锁死！");
    }

    // ==========================================
    // 辅助工具：提取候选标签池供 UI 展示
    // ==========================================
    public List<SubTag> GetTagChoicesForTask(ActiveLootTask task, int count)
    {
        var pool = GetTagPoolForTask(task.Config);
        return pool.OrderBy(x => Guid.NewGuid()).Take(count).ToList();
    }

    // (以下是从旧代码复用的底层算法，保持不变)
    private List<SubTag> GetTagPoolForTask(LootTaskConfig task)
    {
        if (task.PoolSource == TagPoolSource.CustomSubTags) return task.CustomSubTagMix.Distinct().ToList();
        var targetMacros = GetTargetMacrosForTask(task);
        var validComps = PlayerInventoryManager.Instance.AllComponentDatabase.Where(c => targetMacros.Contains(c.MacroCategory)).ToList();
        HashSet<SubTag> pool = new HashSet<SubTag>();
        foreach (var c in validComps) foreach (var t in c.BaseSubTags) pool.Add(t);
        return pool.ToList();
    }

    private List<MacroCategory> GetTargetMacrosForTask(LootTaskConfig task)
    {
        if (task.PoolSource == TagPoolSource.CustomMacros && task.CustomMacroMix.Count > 0) return task.CustomMacroMix.Distinct().ToList();
        return new List<MacroCategory> { currentMacroContext };
    }

    // 👇 接收 task 参数，读取策划配的等级权重
    private List<InstancedComponent> GenerateLoot(Func<ComponentDataSO, bool> filter, LootTaskConfig task, int depth)
    {
        var validBps = PlayerInventoryManager.Instance.AllComponentDatabase.Where(filter).ToList();
        if (validBps.Count == 0) return new List<InstancedComponent>();

        int targetCount = UnityEngine.Random.value <= task.TripleChoiceProbability ? 3 : 1;
        var drawn = validBps.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        // 调用 RollLevel 时，把 task 传进去读权重
        return drawn.Select(bp => new InstancedComponent(bp, RollLevel(bp, task))).ToList();
    }

    private List<InstancedComponent> GenerateCustomLoot(List<CustomDropEntry> customPool, float tripleProb)
    {
        if (customPool == null || customPool.Count == 0) return new List<InstancedComponent>();
        int targetCount = UnityEngine.Random.value <= tripleProb ? 3 : 1;
        var drawn = customPool.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();
        return drawn.Select(entry => new InstancedComponent(entry.Blueprint, entry.Level)).ToList();
    }

    // 核心：完全读取策划配置的等级权重！
    private int RollLevel(ComponentDataSO bp, LootTaskConfig task)
    {
        int w1 = task.Weight_Lv1;
        int w2 = task.Weight_Lv2;
        int w3 = task.Weight_Lv3;
        int w4 = task.Weight_Lv4;

        // 图纸防污染保护 (如果该图纸最低爆2级，清空1级权重)
        if (bp.MinDropLevel > 1) w1 = 0;
        if (bp.MinDropLevel > 2) w2 = 0;
        if (bp.MinDropLevel > 3) w3 = 0;

        int totalW = w1 + w2 + w3 + w4;
        if (totalW <= 0) return bp.MinDropLevel; // 兜底

        int roll = UnityEngine.Random.Range(0, totalW);
        if (roll < w1) return 1;
        if (roll < w1 + w2) return 2;
        if (roll < w1 + w2 + w3) return 3;
        return 4;
    }
}