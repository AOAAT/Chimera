using System.Collections.Generic;
using UnityEngine;

// 1. 节点类型枚举 (对应您设想的敌人、事件等)
public enum MapNodeType
{
    Start,      // 起点
    Enemy,      // 普通敌人 (图纸/废件掉落)
    Elite,      // 精英敌人 (高阶图纸掉落)
    Event,      // 随机事件 (问号)
    Workshop,   // 车间 (回血/修理机甲)
    Boss        // 关底首领
}

// 2. 节点状态枚举 (控制UI如何显示它)
public enum MapNodeState
{
    Locked,     // 尚未解锁 (暗色)
    Selectable, // 当前可前往 (呼吸高亮)
    Passed      // 已经走过 (变灰/打勾)
}

// 3. 核心数据类
[System.Serializable]
public class MapNodeData
{
    public string NodeID;           // 唯一ID
    public int LayerIndex;          // 属于第几层 (0 是起点)
    public MapNodeType NodeType;    // 节点类型
    public MapNodeState NodeState;  // 当前状态

    // 逻辑坐标 (用于UI排版计算，X为横向分布，Y为层级高度)
    public Vector2 LogicalPosition;

    // 上下游连线关系 (存储ID串联整张网)
    public List<string> NextNodeIDs = new List<string>();
    public List<string> PrevNodeIDs = new List<string>();

    // 构造函数
    public MapNodeData(int layer, int indexInLayer, MapNodeType type)
    {
        NodeID = $"Node_{layer}_{indexInLayer}";
        LayerIndex = layer;
        NodeType = type;
        NodeState = MapNodeState.Locked;
    }
}