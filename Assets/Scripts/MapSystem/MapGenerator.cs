using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("=== 地图生成参数 ===")]
    public int TotalLayers = 15;        // 包含起点和Boss的总层数
    public int MinNodesPerLayer = 2;    // 每层最少节点数
    public int MaxNodesPerLayer = 4;    // 每层最多节点数

    [Header("=== 节点类型权重 (总和随意) ===")]
    public float EnemyWeight = 60f;
    public float EventWeight = 25f;
    public float EliteWeight = 10f;
    public float WorkshopWeight = 5f;


    // 👇【新增】：有机浮动参数 (数值越大，地图越扭曲)
    [Header("=== 有机浮动 (Organic Jitter) ===")]
    [Range(0f, 1f)] public float JitterX = 0.6f; // 横向最大偏移量
    [Range(0f, 0.5f)] public float JitterY = 0.2f; // 纵向最大偏移量 (不宜过大，防止层级颠倒)

    // 内存中的完整地图字典：Key 为 NodeID
    public Dictionary<string, MapNodeData> GeneratedMap { get; private set; }

    // 暴露出生成接口
    public void GenerateNewMap()
    {
        GeneratedMap = new Dictionary<string, MapNodeData>();
        List<List<MapNodeData>> layers = new List<List<MapNodeData>>();

        // --- 1. 生成所有节点实体 ---
        for (int i = 0; i < TotalLayers; i++)
        {
            List<MapNodeData> currentLayerNodes = new List<MapNodeData>();

            // 起点(第0层)和终点(最后一层)固定只有1个节点
            int nodeCount = (i == 0 || i == TotalLayers - 1) ? 1 : Random.Range(MinNodesPerLayer, MaxNodesPerLayer + 1);

            for (int j = 0; j < nodeCount; j++)
            {
                MapNodeType type = DetermineNodeType(i);
                MapNodeData newNode = new MapNodeData(i, j, type);

                // 1. 计算绝对标准的网格坐标
                float base_X = (j - (nodeCount - 1) / 2f) * 2f;
                float base_Y = i;

                // 2. 👇【核心注入】：有机扰动偏移！
                // 保护机制：起点 (第0层) 和 Boss点 (最后一层) 绝对居中，不加偏移，稳住地图大局
                if (i > 0 && i < TotalLayers - 1)
                {
                    base_X += Random.Range(-JitterX, JitterX);
                    base_Y += Random.Range(-JitterY, JitterY);
                }

                // 3. 赋值给节点
                newNode.LogicalPosition = new Vector2(base_X, base_Y);

                currentLayerNodes.Add(newNode);
                GeneratedMap.Add(newNode.NodeID, newNode);
            }
            layers.Add(currentLayerNodes);
        }

        // --- 2. 编织树状连线 (防交叉核心算法) ---
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

        Debug.Log($"【地图大脑】系统自检完毕：成功生成了 {GeneratedMap.Count} 个探索节点！");
    }

    private void ConnectNodes(MapNodeData from, MapNodeData to)
    {
        if (!from.NextNodeIDs.Contains(to.NodeID)) from.NextNodeIDs.Add(to.NodeID);
        if (!to.PrevNodeIDs.Contains(from.NodeID)) to.PrevNodeIDs.Add(from.NodeID);
    }

    private MapNodeType DetermineNodeType(int layerIndex)
    {
        if (layerIndex == 0) return MapNodeType.Start;
        if (layerIndex == TotalLayers - 1) return MapNodeType.Boss;
        // 可以在中后期强制刷一个车间让玩家喘口气
        //if (layerIndex == TotalLayers / 2) return MapNodeType.Workshop;

        float totalWeight = EnemyWeight + EventWeight + EliteWeight + WorkshopWeight;
        float roll = Random.Range(0, totalWeight);

        if (roll < EnemyWeight) return MapNodeType.Enemy;
        roll -= EnemyWeight;
        if (roll < EventWeight) return MapNodeType.Event;
        roll -= EventWeight;
        if (roll < EliteWeight) return MapNodeType.Elite;
        return MapNodeType.Workshop;
    }
}