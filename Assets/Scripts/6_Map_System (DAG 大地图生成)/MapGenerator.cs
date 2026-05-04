using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("=== 1. 地图结构参数 ===")]
    public int TotalLayers = 15;
    public int MinNodesPerLayer = 2;
    public int MaxNodesPerLayer = 4;

    [Header("=== 2. 全局节点生成权重 (确定地图上显示什么) ===")]
    public float Weight_NormalEnemy = 40f;
    public float Weight_Unknown = 30f;  // 👈 问号房
    public float Weight_Event = 10f;    // 👈 天然显现的事件
    public float Weight_Elite = 10f;
    public float Weight_Workshop = 10f;

    [Header("=== 3. 问号房内核分布权重 (确定揭开后是什么) ===")]
    public float InnerWeight_Event = 50f;
    public float InnerWeight_Workshop = 15f;
    public float InnerWeight_Elite = 5f;
    [Space]
    public float InnerWeight_TechBattle = 10f;
    public float InnerWeight_FleshBattle = 10f;
    public float InnerWeight_MagicBattle = 5f;
    public float InnerWeight_MixedBattle = 5f;

    [Header("=== 4. 普通战斗流派均衡器 (防扎堆) ===")]
    [Range(0f, 1f)] public float BufferFactor = 0.2f;
    public float TechBase = 10f;
    public float FleshBase = 10f;
    public float MagicBase = 10f;
    public float MixedBase = 2f;

    [Header("=== 5. 视觉随机扰动 ===")]
    [Range(0f, 1f)] public float JitterX = 0.6f;
    [Range(0f, 0.5f)] public float JitterY = 0.2f;

    public Dictionary<string, MapNodeData> GeneratedMap { get; private set; }
    private Dictionary<MapNodeType, int> missCounts;

    public void GenerateNewMap()
    {
        GeneratedMap = new Dictionary<string, MapNodeData>();
        List<List<MapNodeData>> layers = new List<List<MapNodeData>>();

        missCounts = new Dictionary<MapNodeType, int>
        {
            { MapNodeType.Enemy_Tech, 0 }, { MapNodeType.Enemy_Flesh, 0 },
            { MapNodeType.Enemy_Magic, 0 }, { MapNodeType.Enemy_Mixed, 0 }
        };

        for (int i = 0; i < TotalLayers; i++)
        {
            List<MapNodeData> currentLayerNodes = new List<MapNodeData>();
            int nodeCount = (i == 0 || i == TotalLayers - 1) ? 1 : Random.Range(MinNodesPerLayer, MaxNodesPerLayer + 1);

            for (int j = 0; j < nodeCount; j++)
            {
                MapNodeType visualType = DetermineNodeType(i);
                MapNodeData newNode = new MapNodeData(i, j, visualType);

                // --- 👇【核心重构：多维度内核预解算】 ---
                if (visualType == MapNodeType.Unknown)
                {
                    newNode.IsRevealed = false;
                    newNode.HiddenRealType = RollUnknownInnerType();
                }
                else
                {
                    newNode.HiddenRealType = visualType;
                    newNode.IsRevealed = true;
                }
                // ------------------------------------------

                float base_X = (j - (nodeCount - 1) / 2f) * 2f;
                float base_Y = i;
                if (i > 0 && i < TotalLayers - 1)
                {
                    base_X += Random.Range(-JitterX, JitterX);
                    base_Y += Random.Range(-JitterY, JitterY);
                }
                newNode.LogicalPosition = new Vector2(base_X, base_Y);

                currentLayerNodes.Add(newNode);
                GeneratedMap.Add(newNode.NodeID, newNode);
            }
            layers.Add(currentLayerNodes);
        }

        ExecuteZipperConnection(layers);
    }

    private MapNodeType DetermineNodeType(int layerIndex)
    {
        if (layerIndex == 0) return MapNodeType.Start;
        if (layerIndex == TotalLayers - 1) return MapNodeType.Boss;

        float total = Weight_NormalEnemy + Weight_Unknown + Weight_Event + Weight_Elite + Weight_Workshop;
        float roll = Random.Range(0, total);

        if (roll < Weight_NormalEnemy) return RollBalancedNormalEnemy();
        roll -= Weight_NormalEnemy;
        if (roll < Weight_Unknown) return MapNodeType.Unknown;
        roll -= Weight_Unknown;
        if (roll < Weight_Event) return MapNodeType.Event;
        roll -= Weight_Event;
        if (roll < Weight_Elite) return MapNodeType.Elite;
        return MapNodeType.Workshop;
    }

    // 计算问号房背后隐藏的真实身份
    private MapNodeType RollUnknownInnerType()
    {
        float total = InnerWeight_Event + InnerWeight_Workshop + InnerWeight_Elite +
                      InnerWeight_TechBattle + InnerWeight_FleshBattle +
                      InnerWeight_MagicBattle + InnerWeight_MixedBattle;

        float roll = Random.Range(0, total);

        if (roll < InnerWeight_Event) return MapNodeType.Event;
        roll -= InnerWeight_Event;
        if (roll < InnerWeight_Workshop) return MapNodeType.Workshop;
        roll -= InnerWeight_Workshop;
        if (roll < InnerWeight_Elite) return MapNodeType.Elite;
        roll -= InnerWeight_Elite;

        // 细分的普通战斗
        if (roll < InnerWeight_TechBattle) return MapNodeType.Enemy_Tech;
        roll -= InnerWeight_TechBattle;
        if (roll < InnerWeight_FleshBattle) return MapNodeType.Enemy_Flesh;
        roll -= InnerWeight_FleshBattle;
        if (roll < InnerWeight_MagicBattle) return MapNodeType.Enemy_Magic;

        return MapNodeType.Enemy_Mixed;
    }

    private MapNodeType RollBalancedNormalEnemy()
    {
        float wTech = TechBase * (1f + missCounts[MapNodeType.Enemy_Tech] * BufferFactor);
        float wFlesh = FleshBase * (1f + missCounts[MapNodeType.Enemy_Flesh] * BufferFactor);
        float wMagic = MagicBase * (1f + missCounts[MapNodeType.Enemy_Magic] * BufferFactor);
        float wMixed = MixedBase * (1f + missCounts[MapNodeType.Enemy_Mixed] * BufferFactor);

        float totalW = wTech + wFlesh + wMagic + wMixed;
        float roll = Random.Range(0, totalW);

        MapNodeType selected = MapNodeType.Enemy_Tech;
        if (roll < wTech) selected = MapNodeType.Enemy_Tech;
        else if (roll < wTech + wFlesh) selected = MapNodeType.Enemy_Flesh;
        else if (roll < wTech + wFlesh + wMagic) selected = MapNodeType.Enemy_Magic;
        else selected = MapNodeType.Enemy_Mixed;

        missCounts[MapNodeType.Enemy_Tech]++; missCounts[MapNodeType.Enemy_Flesh]++;
        missCounts[MapNodeType.Enemy_Magic]++; missCounts[MapNodeType.Enemy_Mixed]++;
        missCounts[selected] = 0;
        return selected;
    }

    private void ExecuteZipperConnection(List<List<MapNodeData>> layers)
    {
        for (int i = 0; i < TotalLayers - 1; i++)
        {
            List<MapNodeData> currLayer = layers[i];
            List<MapNodeData> nextLayer = layers[i + 1];
            int c = 0, n = 0;
            ConnectNodes(currLayer[c], nextLayer[n]);
            while (c < currLayer.Count - 1 || n < nextLayer.Count - 1)
            {
                bool canMoveC = c < currLayer.Count - 1;
                bool canMoveN = n < nextLayer.Count - 1;
                if (canMoveC && canMoveN)
                {
                    float maxC = Mathf.Max(1, currLayer.Count - 1);
                    float maxN = Mathf.Max(1, nextLayer.Count - 1);
                    float dC = Mathf.Abs((c + 1) / maxC - n / maxN);
                    float dN = Mathf.Abs(c / maxC - (n + 1) / maxN);
                    if (dC < dN) c++; else if (dN < dC) n++; else { if (Random.value < 0.5f) c++; else n++; }
                }
                else if (canMoveC) c++; else if (canMoveN) n++;
                ConnectNodes(currLayer[c], nextLayer[n]);
            }
        }
    }

    private void ConnectNodes(MapNodeData from, MapNodeData to)
    {
        if (!from.NextNodeIDs.Contains(to.NodeID)) from.NextNodeIDs.Add(to.NodeID);
        if (!to.PrevNodeIDs.Contains(from.NodeID)) to.PrevNodeIDs.Add(from.NodeID);
    }
}