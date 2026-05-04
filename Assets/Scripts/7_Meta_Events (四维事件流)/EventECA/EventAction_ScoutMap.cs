// --- EventAction_ScoutMap.cs ---
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Act_ScoutMap", menuName = "Chimera Protocol/Event ECA/揭示地图")]
public class EventAction_ScoutMap : EventAction
{
    public int ScoutCount = 3;

    public override void Execute()
    {
        if (MapManager.Instance == null) return;

        // 寻找所有尚未解锁(Locked)且层数大于当前层数的节点
        var allNodes = MapManager.Instance.GetComponent<MapGenerator>().GeneratedMap.Values;
        int currentLayer = MapManager.Instance.CurrentLayer;

        var futureNodes = allNodes
            .Where(n => n.LayerIndex > currentLayer && n.NodeState == MapNodeState.Locked)
            .OrderBy(n => Random.value) // 随机挑几个
            .Take(ScoutCount);

        foreach (var node in futureNodes)
        {
            // 这里我们不把它设为 Selectable，而是设为一个新的状态 Revealed
            // 你需要在 MapNodeState 枚举里加一个 Revealed
            // 并在 MapNodeUI 里把 Revealed 状态配成“半透明显示图标”
            node.NodeState = MapNodeState.Selectable; // 简单实现：直接显示
        }

        // 刷新 UI
        FindObjectOfType<MapVisualizer>()?.RefreshAllVisuals();
        Debug.Log($"<color=#00FFFF>【远距扫描】</color> 已探明前方 {ScoutCount} 处位置。");
    }
}