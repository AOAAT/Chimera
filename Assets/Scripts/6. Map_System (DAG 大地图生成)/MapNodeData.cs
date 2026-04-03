using System.Collections.Generic;
using UnityEngine;

public enum MapNodeType { Start, Enemy_Tech, Enemy_Flesh, Enemy_Magic, Enemy_Mixed, Elite, Boss, Event, Workshop }
public enum MapNodeState { Locked, Selectable, Passed }

[System.Serializable]
public class MapNodeData
{
    public string NodeID;
    public int LayerIndex;
    public MapNodeType NodeType;
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
    }
}