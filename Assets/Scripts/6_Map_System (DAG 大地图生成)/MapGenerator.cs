using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("=== 地图生成参数 ===")]
    public int TotalLayers = 15;
    public int MinNodesPerLayer = 2;
    public int MaxNodesPerLayer = 4;

    [Header("=== 大类权重 (是否生成普通战斗) ===")]
    public float NormalEnemyWeight = 60f;
    public float EventWeight = 25f;
    public float EliteWeight = 10f;
    public float WorkshopWeight = 5f;

    [Header("=== 普通战斗内部均衡 (防扎堆) ===")]
    [Range(0f, 1f)] public float BufferFactor = 0.2f;
    public float TechBaseWeight = 10f;
    public float FleshBaseWeight = 10f;
    public float MagicBaseWeight = 10f;
    public float MixedBaseWeight = 2f;

    [Header("=== 有机浮动 ===")]
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
                MapNodeType finalType = DetermineNodeType(i);
                MapNodeData newNode = new MapNodeData(i, j, finalType);

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

        float totalMacroW = NormalEnemyWeight + EventWeight + EliteWeight + WorkshopWeight;
        float roll = Random.Range(0, totalMacroW);

        if (roll < NormalEnemyWeight) return RollBalancedNormalEnemy();
        roll -= NormalEnemyWeight;
        if (roll < EventWeight) return MapNodeType.Event;
        roll -= EventWeight;
        if (roll < EliteWeight) return MapNodeType.Elite;
        return MapNodeType.Workshop;
    }

    private MapNodeType RollBalancedNormalEnemy()
    {
        float wTech = TechBaseWeight * (1f + missCounts[MapNodeType.Enemy_Tech] * BufferFactor);
        float wFlesh = FleshBaseWeight * (1f + missCounts[MapNodeType.Enemy_Flesh] * BufferFactor);
        float wMagic = MagicBaseWeight * (1f + missCounts[MapNodeType.Enemy_Magic] * BufferFactor);
        float wMixed = MixedBaseWeight * (1f + missCounts[MapNodeType.Enemy_Mixed] * BufferFactor);

        float totalW = wTech + wFlesh + wMagic + wMixed;
        float roll = Random.Range(0, totalW);
        MapNodeType selectedType = MapNodeType.Enemy_Tech;

        if (roll < wTech) selectedType = MapNodeType.Enemy_Tech;
        else if (roll < wTech + wFlesh) selectedType = MapNodeType.Enemy_Flesh;
        else if (roll < wTech + wFlesh + wMagic) selectedType = MapNodeType.Enemy_Magic;
        else selectedType = MapNodeType.Enemy_Mixed;

        missCounts[MapNodeType.Enemy_Tech]++;
        missCounts[MapNodeType.Enemy_Flesh]++;
        missCounts[MapNodeType.Enemy_Magic]++;
        missCounts[MapNodeType.Enemy_Mixed]++;
        missCounts[selectedType] = 0;
        return selectedType;
    }

    // ==========================================
    // 👇【核心重构】：智能引力拉链算法 (完美回归中路，绝对不交叉)
    // ==========================================
    private void ExecuteZipperConnection(List<List<MapNodeData>> layers)
    {
        for (int i = 0; i < TotalLayers - 1; i++)
        {
            List<MapNodeData> currLayer = layers[i];
            List<MapNodeData> nextLayer = layers[i + 1];

            int c = 0;
            int n = 0;

            // 1. 永远先连接双方最左侧的初始点
            ConnectNodes(currLayer[c], nextLayer[n]);

            // 2. 只要还没拉到最右侧，就继续拉
            while (c < currLayer.Count - 1 || n < nextLayer.Count - 1)
            {
                bool canMoveC = c < currLayer.Count - 1;
                bool canMoveN = n < nextLayer.Count - 1;

                if (canMoveC && canMoveN)
                {
                    // 核心魔法：计算推进 C 和推进 N 的“进度偏差比”
                    float maxC = Mathf.Max(1, currLayer.Count - 1);
                    float maxN = Mathf.Max(1, nextLayer.Count - 1);

                    float currentCRatio = c / maxC;
                    float currentNRatio = n / maxN;

                    float diffIfMoveC = Mathf.Abs((c + 1) / maxC - currentNRatio);
                    float diffIfMoveN = Mathf.Abs(currentCRatio - (n + 1) / maxN);

                    // 谁推进一步后的偏差更小（更靠近中心线），就推进谁！
                    // 这就强制把边缘的孤岛节点，硬生生地拉向了对面的中心节点！
                    if (diffIfMoveC < diffIfMoveN)
                    {
                        c++;
                    }
                    else if (diffIfMoveN < diffIfMoveC)
                    {
                        n++;
                    }
                    else
                    {
                        // 如果偏差一样（完美对称时），随机选择向左或向右推进，形成漂亮的菱形网状分支！
                        if (Random.value < 0.5f) c++;
                        else n++;
                    }
                }
                else if (canMoveC) c++;
                else if (canMoveN) n++;

                // 连接推进后的新节点！
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