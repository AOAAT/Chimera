// --- START OF FILE LootSequenceDirector.cs ---
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LootSequenceDirector : MonoBehaviour
{
    public static LootSequenceDirector Instance;

    private void Awake() { if (Instance == null) Instance = this; }

    public async void StartLootSequence(LootSequenceSO lootConfig, MacroCategory macro, int mapDepth)
    {
        Debug.Log("<color=#FFD700>【打捞管线启动】</color> 开始执行战利品任务链...");

        // 1. 等待玩家把所有的选牌任务做完
        foreach (var task in lootConfig.Tasks)
        {
            await ProcessSingleTask(task, macro, mapDepth);
        }

        // 2. 👇【完美闭环】：所有 UI 选完了，大巴扎关门，返回大地图！
        Debug.Log("<color=#00FF00>【打捞管线结束】</color> 战利品发放完毕！准备返回大地图。");

        // 呼叫战斗导演，让他执行“撤掉战斗背景、显示地图卷轴”的操作
        CombatDirector.Instance.ExecuteReturnToMap();
    }
    private async Task ProcessSingleTask(LootTaskConfig task, MacroCategory macro, int depth)
    {
        List<InstancedComponent> generatedLoot = new List<InstancedComponent>();

        switch (task.Mode)
        {
            case LootDropMode.MacroCategorySingle:
                generatedLoot = GenerateLoot(c => c.MacroCategory == macro, 0f, depth);
                break;

            case LootDropMode.SystemAssignedTag:
                SubTag sysTag = GetRandomSubTagFromPool(macro, 1)[0];
                generatedLoot = GenerateLoot(c => c.BaseSubTags.Contains(sysTag), task.TripleChoiceProbability, depth);
                break;

            case LootDropMode.PlayerDrivenFilter:
                List<SubTag> tagChoices = GetRandomSubTagFromPool(macro, 3);
                // 👇【异步等待UI】：呼叫 UI 弹窗，代码在此处挂起，直到玩家点完按钮！
                SubTag playerChosenTag = await LootUIManager.Instance.RequestTagSelection(tagChoices);
                generatedLoot = GenerateLoot(c => c.BaseSubTags.Contains(playerChosenTag), task.TripleChoiceProbability, depth);
                break;

            case LootDropMode.CustomPoolDrop:
                // 👇【新增】：完全无视动态算法，直接从策划配好的池子里捞！
                generatedLoot = GenerateCustomLoot(task.CustomPool, task.TripleChoiceProbability);
                break;
        }

        if (generatedLoot != null && generatedLoot.Count > 0)
        {
            // 👇【异步等待UI】：呼叫 UI 展出物品，代码挂起，等待玩家挑选带走！
            InstancedComponent claimedItem = await LootUIManager.Instance.RequestItemSelection(generatedLoot);

            if (claimedItem != null)
            {
                PlayerInventoryManager.Instance.ComponentInventory.Add(claimedItem);
                PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
                Debug.Log($"【入库成功】获得 Lv.{claimedItem.CurrentLevel} [{claimedItem.BaseData.ComponentName}]");
            }
        }
    }

    // --- 标准掉落算法 (根据深度自动定等级) ---
    private List<InstancedComponent> GenerateLoot(Func<ComponentDataSO, bool> filter, float tripleProb, int depth)
    {
        var validBps = PlayerInventoryManager.Instance.AllComponentDatabase.Where(filter).ToList();
        if (validBps.Count == 0) return new List<InstancedComponent>();

        int targetCount = UnityEngine.Random.value <= tripleProb ? 3 : 1;
        var drawn = validBps.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        return drawn.Select(bp => new InstancedComponent(bp, RollLevel(bp, depth))).ToList();
    }

    // --- 定制掉落算法 (完全听从策划配置) ---
    private List<InstancedComponent> GenerateCustomLoot(List<CustomDropEntry> customPool, float tripleProb)
    {
        if (customPool == null || customPool.Count == 0) return new List<InstancedComponent>();

        int targetCount = UnityEngine.Random.value <= tripleProb ? 3 : 1;

        // 随机抽，但直接读取策划配好的 Level！
        var drawn = customPool.OrderBy(x => Guid.NewGuid()).Take(targetCount).ToList();

        return drawn.Select(entry => new InstancedComponent(entry.Blueprint, entry.Level)).ToList();
    }

    private int RollLevel(ComponentDataSO bp, int depth) { return bp.MinDropLevel; /* 简化版，可接你之前的权重逻辑 */ }

    private List<SubTag> GetRandomSubTagFromPool(MacroCategory macro, int count)
    {
        var validComps = PlayerInventoryManager.Instance.AllComponentDatabase.Where(c => c.MacroCategory == macro).ToList();
        HashSet<SubTag> pool = new HashSet<SubTag>();
        foreach (var c in validComps) foreach (var t in c.BaseSubTags) pool.Add(t);
        return pool.OrderBy(x => Guid.NewGuid()).Take(count).ToList();
    }
}