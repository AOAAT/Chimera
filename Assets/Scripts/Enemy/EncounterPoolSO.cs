using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEncounterPool", menuName = "Chimera Protocol/Combat/Encounter Pool (遭遇战牌库)")]
public class EncounterPoolSO : ScriptableObject
{
    [Header("=== 1. 牌库调度权重 ===")]
    [Tooltip("当满足条件时，该牌库被选中的概率权重")]
    public float PoolWeight = 10f;

    [Header("=== 2. 牌库适用环境 (Filter Criteria) ===")]
    public int TargetStage = 1;
    public int MinDepth = 0;
    public int MaxDepth = 15;

    [Tooltip("这个牌库适用于哪些节点？(如：勾选 Enemy_Tech，代表它是科技怪牌库)")]
    public List<MapNodeType> AllowedNodeTypes = new List<MapNodeType> { MapNodeType.Enemy_Tech };

    [Header("=== 3. 基础牌库 (手工房间蓝图) ===")]
    public List<EncounterLayoutSO> LayoutList = new List<EncounterLayoutSO>();
    // 运行时的暗箱数据
    [NonSerialized] private List<EncounterLayoutSO> drawPile = new List<EncounterLayoutSO>();
    [NonSerialized] private List<EncounterLayoutSO> discardPile = new List<EncounterLayoutSO>();
    [NonSerialized] private bool isInitialized = false;

    // 引擎初始化 / 洗牌逻辑 (懒加载模式)
    public void InitializePool()
    {
        drawPile.Clear();
        discardPile.Clear();
        if (LayoutList.Count == 0) return;

        drawPile.AddRange(LayoutList);
        ShufflePile(drawPile);
        isInitialized = true;
        Debug.Log($"【牌库洗牌】已将 {drawPile.Count} 套阵型装入 [{this.name}] 抽牌堆！");
    }

    // 发牌逻辑
    public EncounterLayoutSO GetNextEncounter()
    {
        if (LayoutList.Count == 0) return null;

        if (!isInitialized) InitializePool();

        if (drawPile.Count == 0)
        {
            Debug.Log($"【牌库空仓】[{this.name}] 触发洗牌机制！");
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShufflePile(drawPile);
        }

        EncounterLayoutSO drawnEncounter = drawPile[0];
        drawPile.RemoveAt(0);
        discardPile.Add(drawnEncounter);

        return drawnEncounter;
    }

    private void ShufflePile(List<EncounterLayoutSO> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            EncounterLayoutSO temp = pile[i];
            int randomIndex = UnityEngine.Random.Range(i, pile.Count);
            pile[i] = pile[randomIndex];
            pile[randomIndex] = temp;
        }
    }
}