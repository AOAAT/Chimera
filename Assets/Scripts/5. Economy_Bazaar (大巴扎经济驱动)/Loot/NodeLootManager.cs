using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class NodeLootPoolConfig
{
    public int TargetStage = 1;
    public int MinDepth = 0;
    public int MaxDepth = 15;

    [Tooltip("哪些节点适用此掉落补偿？")]
    public List<MapNodeType> AllowedNodeTypes = new List<MapNodeType>();

    [Tooltip("从这些掉落序列中随机抽一个发给玩家")]
    public List<LootSequenceSO> LootSequences = new List<LootSequenceSO>();
}

public class NodeLootManager : MonoBehaviour
{
    public static NodeLootManager Instance;

    [Header("=== 全局节点战利品库 ===")]
    public List<NodeLootPoolConfig> GlobalNodeLootPools = new List<NodeLootPoolConfig>();

    private void Awake() { if (Instance == null) Instance = this; }

    public LootSequenceSO GetLootForNode(int stage, int layer, MapNodeType type)
    {
        var validPools = GlobalNodeLootPools.Where(p =>
            p.TargetStage == stage &&
            layer >= p.MinDepth && layer <= p.MaxDepth &&
            p.AllowedNodeTypes.Contains(type)
        ).ToList();

        if (validPools.Count == 0 || validPools[0].LootSequences.Count == 0) return null;

        // 如果有多个匹配，随便抽一个；这里取第一个匹配池子里的随机一条序列
        var selectedPool = validPools[UnityEngine.Random.Range(0, validPools.Count)];
        return selectedPool.LootSequences[UnityEngine.Random.Range(0, selectedPool.LootSequences.Count)];
    }
}