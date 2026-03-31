using System.Collections.Generic;
using UnityEngine;

// 1. 节点类型枚举
public enum MapNodeType
{
    Start,
    Enemy,
    Elite,
    Event,
    Workshop,
    Boss
}

// 2. 节点状态枚举
public enum MapNodeState
{
    Locked,
    Selectable,
    Passed
}

// 3. 节点阵营主题
public enum NodeTheme
{
    None,       // 无主题 (起点/事件/车间)
    General,    // 通用阵营
    Tech,       // 机械科技
    Flesh,      // 血肉畸变
    Magic,      // 遗迹魔法
    Mixed       // 混合大乱斗
}

[System.Serializable]
public class MapNodeData
{
    public string NodeID;
    public int LayerIndex;
    public MapNodeType NodeType;
    public MapNodeState NodeState;

    // === 阵营与视觉数据 ===
    public NodeTheme Theme;
    public Sprite DisplayIcon;

    // 👇【核心修复】：这就是 CombatDirector 在找的“宏观节点掉落补偿池”！
    public LootSequenceSO NodeLoot;

    // === 物理与连线数据 ===
    public Vector2 LogicalPosition;
    public List<string> NextNodeIDs = new List<string>();
    public List<string> PrevNodeIDs = new List<string>();

    public MapNodeData(int layer, int indexInLayer, MapNodeType type, NodeTheme theme)
    {
        NodeID = $"Node_{layer}_{indexInLayer}";
        LayerIndex = layer;
        NodeType = type;
        Theme = theme;
        NodeState = MapNodeState.Locked;
    }
}