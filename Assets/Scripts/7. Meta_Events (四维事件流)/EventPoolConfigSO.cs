using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEventPool", menuName = "Chimera Protocol/Event System/Event Pool Config")]
public class EventPoolConfigSO : ScriptableObject
{
    [Header("=== 1. 基础权重 ===")]
    [Tooltip("当存在多个符合条件的事件池时，权重越高的池子越容易被抽中。")]
    public float PoolWeight = 10f;

    [Header("=== 2. 筛选条件 (Filter Criteria) ===")]
    public int TargetStage = 1; // 适用的大阶段 (如 Stage 1)

    [Tooltip("适用的最小层数 (LayerIndex)")]
    public int MinDepth = 0;
    [Tooltip("适用的最大层数 (LayerIndex)")]
    public int MaxDepth = 15;

    [Header("=== 3. 事件库 ===")]
    [Tooltip("当此事件池被抽中时，会从以下事件中随机抽取一个呈现给玩家。")]
    public List<EventNodeSO> Events = new List<EventNodeSO>();
}