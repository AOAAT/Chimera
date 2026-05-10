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
        MusicManager.Instance?.SwitchState(MusicState.Loot);

        Debug.Log("<color=#FFD700>【打捞管线启动】</color> 正在合并双源战利品...");

        // 1. 熔炉合并 (将微观遭遇战和宏观节点补偿合并)
        List<LootTaskConfig> combinedConfigs = new List<LootTaskConfig>();
        if (encounterLoot != null) combinedConfigs.AddRange(encounterLoot.Tasks);
        if (nodeLoot != null) combinedConfigs.AddRange(nodeLoot.Tasks);

        // 2. 同类排序 (优先级：保底单抽 -> 盲盒 -> 自选标签 -> 定制极品)
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

        // 🌟 核心修改：清空旧数据，准备直接写入
        task.GeneratedItems.Clear();
        task.GeneratedChassis.Clear();

        switch (task.Config.Mode)
        {
            case LootDropMode.MacroCategorySingle:
                var macros = GetTargetMacrosForTask(task.Config);
                task.GeneratedItems = GenerateLoot(c => macros.Contains(c.MacroCategory), task.Config, currentDepthContext);
                break;

            case LootDropMode.SystemAssignedTag:
                var tagPool = GetTagPoolForTask(task.Config);
                if (tagPool.Count > 0)
                {
                    SubTag sysTag = tagPool[UnityEngine.Random.Range(0, tagPool.Count)];
                    task.GeneratedItems = GenerateLoot(c => c.BaseSubTags.Contains(sysTag), task.Config, currentDepthContext);
                }
                break;

            case LootDropMode.PlayerDrivenFilter:
                if (task.LockedTag.HasValue)
                {
                    task.GeneratedItems = GenerateLoot(c => c.BaseSubTags.Contains(task.LockedTag.Value), task.Config, currentDepthContext);
                }
                break;

            case LootDropMode.CustomPoolDrop:
                // 🌟 核心修改：调用统一的自定义生成器，不再使用局部变量 loot
                Internal_ExecuteCustomRoll(task);
                break;
        }

        // 标记已开启
        task.IsBoxOpened = true;

        int totalCount = task.GeneratedItems.Count + task.GeneratedChassis.Count;
        Debug.Log($"<color=green>【盲盒开启】</color> 模式:{task.Config.Mode} | 生成组件:{task.GeneratedItems.Count} | 生成底盘:{task.GeneratedChassis.Count}");
    }
    // ==========================================
    // 专用逻辑：处理包含底盘的自定义奖励
    private void Internal_ExecuteCustomRoll(ActiveLootTask task)
    {
        var pool = task.Config.CustomPool;
        if (pool == null || pool.Count == 0) return;

        // 1. 决定是单抽还是三选一
        int targetCount = UnityEngine.Random.value <= task.Config.TripleChoiceProbability ? 3 : 1;

        // 2. 随机抽取 Entry
        var drawnEntries = pool.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        foreach (var entry in drawnEntries)
        {
            if (entry.ChassisBlueprint != null)
            {
                // 底盘：直接入库，无视等级
                task.GeneratedChassis.Add(new InstancedChassis(entry.ChassisBlueprint));
            }
            else if (entry.ComponentBlueprint != null)
            {
                // 零件：读取 entry.Level，并增加防呆保护 (最小1级)
                int safeLevel = Mathf.Max(1, entry.Level);
                task.GeneratedItems.Add(new InstancedComponent(entry.ComponentBlueprint, safeLevel));

                // Debug.Log($"[自定义掉落] 注入零件: {entry.ComponentBlueprint.ComponentName} | 等级: {safeLevel}");
            }
        }
    }// ==========================================
    private void GenerateCustomLoot(ActiveLootTask task)
    {
        var pool = task.Config.CustomPool;
        if (pool == null || pool.Count == 0) return;

        // 判定是单抽还是三选一
        int targetCount = UnityEngine.Random.value <= task.Config.TripleChoiceProbability ? 3 : 1;
        var drawnEntries = pool.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        foreach (var entry in drawnEntries)
        {
            if (entry.ChassisBlueprint != null)
            {
                // 核心：如果是底盘奖励，实例化底盘实体（无等级）
                task.GeneratedChassis.Add(new InstancedChassis(entry.ChassisBlueprint));
            }
            else if (entry.ComponentBlueprint != null)
            {
                // 如果是组件奖励，按配置等级实例化
                task.GeneratedItems.Add(new InstancedComponent(entry.ComponentBlueprint, entry.Level));
            }
        }
    }

    // ==========================================
    // 辅助工具与权重算法
    // ==========================================
    public List<SubTag> GetTagChoicesForTask(ActiveLootTask task, int count)
    {
        var pool = GetTagPoolForTask(task.Config);
        return pool.OrderBy(x => Guid.NewGuid()).Take(count).ToList();
    }

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

    private List<InstancedComponent> GenerateLoot(Func<ComponentDataSO, bool> filter, LootTaskConfig task, int depth)
    {
        var validBps = PlayerInventoryManager.Instance.AllComponentDatabase.Where(filter).ToList();
        if (validBps.Count == 0) return new List<InstancedComponent>();

        int targetCount = UnityEngine.Random.value <= task.TripleChoiceProbability ? 3 : 1;
        var drawn = validBps.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        return drawn.Select(bp => new InstancedComponent(bp, RollLevel(bp, task))).ToList();
    }

    private int RollLevel(ComponentDataSO bp, LootTaskConfig task)
    {
        int w1 = task.Weight_Lv1;
        int w2 = task.Weight_Lv2;
        int w3 = task.Weight_Lv3;
        int w4 = task.Weight_Lv4;

        if (bp.MinDropLevel > 1) w1 = 0;
        if (bp.MinDropLevel > 2) w2 = 0;
        if (bp.MinDropLevel > 3) w3 = 0;

        int totalW = w1 + w2 + w3 + w4;
        if (totalW <= 0) return bp.MinDropLevel;

        int roll = UnityEngine.Random.Range(0, totalW);
        if (roll < w1) return 1;
        if (roll < w1 + w2) return 2;
        if (roll < w1 + w2 + w3) return 3;
        return 4;
    }

    public void StartImmediateLoot(InstancedComponent item, System.Action onComplete = null)
    {
        LootTaskConfig mockConfig = new LootTaskConfig { Mode = LootDropMode.CustomPoolDrop };
        ActiveLootTask task = new ActiveLootTask { Config = mockConfig, IsBoxOpened = true, GeneratedItems = new List<InstancedComponent> { item } };
        LootUIManager.Instance.OpenHub(new List<ActiveLootTask> { task }, onComplete);
    }
}