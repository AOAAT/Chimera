using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LootGenerator
{
    // ==========================================
    // 大一统核心算法：基于条件的抽取管线
    // ==========================================
    // 注：为了类型安全，这里传入 Func<ComponentDataSO, bool> 作为万能过滤器，
    // 这样无论是过滤 MacroCategory 还是过滤 SubTag，都只用这一套代码！
    public static List<InstancedComponent> GenerateLoot(
        Func<ComponentDataSO, bool> filterCondition,
        float tripleChoiceProbability,
        int currentMapDepth)
    {
        var inventoryMgr = PlayerInventoryManager.Instance;

        // 1. 过滤候选库 (按策划传入的条件，死死咬住目标池)
        var validBlueprints = inventoryMgr.AllComponentDatabase.Where(filterCondition).ToList();

        if (validBlueprints.Count == 0)
        {
            Debug.LogWarning("【掉落警告】该标签下没有配置任何组件图纸，打捞失败！");
            return new List<InstancedComponent>();
        }

        // 2. 概率掷骰子：决定是 1 个还是 3 个？
        bool isTripleChoice = UnityEngine.Random.value <= tripleChoiceProbability;
        int targetCount = isTripleChoice ? 3 : 1;

        // 3. 洗牌与防重复抽取 (Deduplication)
        // 保证三选一的面板上，绝对不会出现两把同名的枪
        var drawnBlueprints = validBlueprints
            .OrderBy(x => Guid.NewGuid()) // 随机打乱
            .Take(targetCount)            // 截取所需数量（如果池子只有2个，Take(3)会自动返回2个，安全！）
            .ToList();

        // 4. 等级加权组装实体
        List<InstancedComponent> finalLoot = new List<InstancedComponent>();
        foreach (var blueprint in drawnBlueprints)
        {
            int rolledLevel = RollLevelForBlueprint(blueprint, currentMapDepth);
            finalLoot.Add(new InstancedComponent(blueprint, rolledLevel));
        }

        return finalLoot;
    }

    // ==========================================
    // 等级掷骰子引擎 (依据图纸 MinDropLevel 和 地图深度)
    // ==========================================
    private static int RollLevelForBlueprint(ComponentDataSO blueprint, int depth)
    {
        // 此处可接你配置在 SO 里的动态权重表
        // 示例伪代码权重：
        int w1 = Mathf.Max(0, 100 - (depth * 10));
        int w2 = depth >= 3 ? 30 + (depth * 5) : 0;
        int w3 = depth >= 6 ? 10 + (depth * 5) : 0;

        // 锁定掉落门槛：如果 MinDropLevel=2，把 1级 的权重清零，绝不污染卡池！
        if (blueprint.MinDropLevel > 1) w1 = 0;
        if (blueprint.MinDropLevel > 2) w2 = 0;

        int totalWeight = w1 + w2 + w3;
        if (totalWeight <= 0) return blueprint.MinDropLevel; // 终极防呆保底

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < w1) return 1;
        if (roll < w1 + w2) return 2;
        return 3;
    }
}