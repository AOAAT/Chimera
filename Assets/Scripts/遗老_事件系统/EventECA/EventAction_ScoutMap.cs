// --- EventAction_ScoutMap.cs ---
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Act_ScoutMap", menuName = "Chimera Protocol/Event ECA/揭示地图")]
public class EventAction_ScoutMap : EventAction
{
    public int ScoutCount = 3;

    public override void Execute()
    {
        var allNodes = MapManager.Instance.GetComponent<MapGenerator>().GeneratedMap.Values;
        int currentLayer = MapManager.Instance.CurrentLayer;

        // 寻找前方所有还没揭示的“问号房”
        var hiddenNodes = allNodes
            .Where(n => n.LayerIndex > currentLayer && n.NodeType == MapNodeType.Unknown && !n.IsRevealed)
            .OrderBy(n => Random.value)
            .Take(ScoutCount);

        foreach (var node in hiddenNodes)
        {
            node.IsRevealed = true; // 探明真相
        }

        // 刷新大地图 UI，问号会瞬间变成商店/事件图标
        FindObjectOfType<MapVisualizer>()?.RefreshAllVisuals();
    }
}