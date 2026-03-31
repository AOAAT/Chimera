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

        private void ExecuteZipperConnection(List<List<MapNodeData>> layers)
    {
        for (int i = 0; i < TotalLayers - 1; i++)
        {
            List<MapNodeData> currLayer = layers[i];
            List<MapNodeData> nextLayer = layers[i + 1];

            int currIdx = 0;
            int nextIdx = 0;

            // 使用双指针拉链法，指针只能向右走，绝对不可能产生物理交叉！
            while (currIdx < currLayer.Count || nextIdx < nextLayer.Count)
            {
                // 1. 连线当前指针指向的两个节点
                ConnectNodes(currLayer[currIdx], nextLayer[nextIdx]);

                // 2. 看看两边是不是还能往右走？
                bool currCanMove = currIdx < currLayer.Count - 1;
                bool nextCanMove = nextIdx < nextLayer.Count - 1;

                if (currCanMove && nextCanMove)
                {
                    // 都在半路上，随机决定谁往右走（或者一起走）
                    float roll = Random.value;
                    if (roll < 0.3f)
                        currIdx++;      // 下面的节点往右走，连向同一个上面的节点 (形成 V 字)
                    else if (roll < 0.6f)
                        nextIdx++;      // 上面的节点往右走，连向同一个下面的节点 (形成倒 V 字)
                    else
                    {
                        currIdx++;
                        nextIdx++;
                    } // 一起往右走，形成两条平行向上的线
                }
                else if (currCanMove)
                {
                    // 上面到头了，下面只能乖乖往右走，全部汇聚到上面的最右侧节点
                    currIdx++;
                }
                else if (nextCanMove)
                {
                    // 下面到头了，上面只能乖乖往右走，全部从下面的最右侧节点出发
                    nextIdx++;
                }
                else
                {
                    // 两边都到头了，本层连线完美收工！
                    break;
                }
            }
        }
    }

    private void ConnectNodes(MapNodeData from, MapNodeData to)
    {
        if (!from.NextNodeIDs.Contains(to.NodeID)) from.NextNodeIDs.Add(to.NodeID);
        if (!to.PrevNodeIDs.Contains(from.NodeID)) to.PrevNodeIDs.Add(from.NodeID);
    }
}