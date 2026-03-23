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

            // 规则A：确保当前层的每个节点，至少连向下一个层的一个节点
            for (int j = 0; j < currLayer.Count; j++)
            {
                // 巧妙的映射算法：根据自身索引按比例找到下一层的对应目标，防止X型交叉
                int targetIndex = Mathf.Clamp(Mathf.RoundToInt((float)j / currLayer.Count * nextLayer.Count), 0, nextLayer.Count - 1);
                ConnectNodes(currLayer[j], nextLayer[targetIndex]);

                // 有概率额外连向旁边的一个节点，增加路线选择的丰富度
                if (Random.value > 0.5f && targetIndex + 1 < nextLayer.Count)
                {
                    ConnectNodes(currLayer[j], nextLayer[targetIndex + 1]);
                }
            }

            // 规则B：反向检查，确保下一层的每个节点都至少有一个入口！(防止出现死节点)
            for (int k = 0; k < nextLayer.Count; k++)
            {
                if (nextLayer[k].PrevNodeIDs.Count == 0)
                {
                    // 如果它没有上游，强行把它连向当前层离它最近的节点
                    int closestIndex = Mathf.Clamp(Mathf.RoundToInt((float)k / nextLayer.Count * currLayer.Count), 0, currLayer.Count - 1);
                    ConnectNodes(currLayer[closestIndex], nextLayer[k]);
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
        if (layerIndex == TotalLayers / 2) return MapNodeType.Workshop;

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