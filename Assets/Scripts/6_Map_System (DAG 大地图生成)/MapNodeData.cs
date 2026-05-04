using System.Collections.Generic;
using UnityEngine;

public enum MapNodeType { Start, Enemy_Tech, Enemy_Flesh, Enemy_Magic, Enemy_Mixed, Elite, Boss, Event, Workshop, Unknown } // 👈 新增 Unknown 类型
public enum MapNodeState { Locked, Selectable, Passed }

[System.Serializable]
public class MapNodeData
{
    public string NodeID;
    public int LayerIndex;
    public MapNodeType NodeType;      // 视觉上显示的类型
    public MapNodeType HiddenRealType; // 👈 问号背后真实的类型（如果是 Unknown 的话）
    public bool IsRevealed = false;    // 👈 是否已被麦田怪圈等手段揭示

    public MapNodeState NodeState;
    public Vector2 LogicalPosition;
    public List<string> NextNodeIDs = new List<string>();
    public List<string> PrevNodeIDs = new List<string>();

    public MapNodeData(int layer, int indexInLayer, MapNodeType type)
    {
        NodeID = $"Node_{layer}_{indexInLayer}";
        LayerIndex = layer;
        NodeType = type;
        NodeState = MapNodeState.Locked;

        // 初始状态：非问号节点默认就是探明的
        if (type != MapNodeType.Unknown) IsRevealed = true;
    }
}