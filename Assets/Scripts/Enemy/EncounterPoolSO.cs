using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEncounterPool", menuName = "Chimera Protocol/Combat/Encounter Pool (敌人池)")]
public class EncounterPoolSO : ScriptableObject
{
    [Header("=== 基础牌库 ===")]
    [Tooltip("把配置好的遭遇战布局 (Layout) 拖到这里")]
    public List<EncounterLayoutSO> LayoutList = new List<EncounterLayoutSO>();

    // 运行时的暗箱数据
    [NonSerialized] private List<EncounterLayoutSO> drawPile = new List<EncounterLayoutSO>();
    [NonSerialized] private List<EncounterLayoutSO> discardPile = new List<EncounterLayoutSO>();

    // 引擎初始化 / 洗牌逻辑
    public void InitializePool()
    {
        drawPile.Clear();
        discardPile.Clear();

        if (LayoutList.Count == 0) return;

        drawPile.AddRange(LayoutList);
        ShufflePile(drawPile);
        Debug.Log($"【敌人池洗牌】已将 {drawPile.Count} 套阵型装入抽牌堆！");
    }

    // 发牌逻辑
    public EncounterLayoutSO GetNextEncounter()
    {
        if (LayoutList.Count == 0)
        {
            Debug.LogError("【致命错误】敌人池是空的！请在配置表里塞入 Layout！");
            return null;
        }

        // 如果牌抽空了，重新把弃牌堆洗入抽牌堆
        if (drawPile.Count == 0)
        {
            Debug.Log("【敌人池空仓】触发洗牌机制！弃牌堆重新归入抽牌堆。");
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShufflePile(drawPile);
        }

        // 抽走最上面的一张
        EncounterLayoutSO drawnEncounter = drawPile[0];
        drawPile.RemoveAt(0);
        discardPile.Add(drawnEncounter);

        return drawnEncounter;
    }

    // 经典的 Fisher-Yates 洗牌算法
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