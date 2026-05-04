using UnityEngine;

[RequireComponent(typeof(MapGenerator))]
public class MapManager : MonoBehaviour
{
    // 单例模式，方便全局随时随地呼叫它
    public static MapManager Instance { get; private set; }

    private MapGenerator mapGenerator;

    [Header("=== 场景遮罩流 ===")]
    public GameObject MapUIPanel; // 👇【新增】：拖入包含地图卷轴的整个大 UI 父节点

    [Header("=== 玩家当前状态 ===")]
    public string CurrentNodeID;
    public int CurrentLayer;

    // 您大纲里提到的全局资源，未来会接管这部分
    //public int CurrentSAN = 100;
    //public int CurrentPower = 50;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        mapGenerator = GetComponent<MapGenerator>();
    }

    private void Start()
    {
        // 测试发车！生成新地图
        StartNewExpedition();
    }

    public void StartNewExpedition()
    {
        mapGenerator.GenerateNewMap();

        // 找到第0层的起点，把玩家扔上去
        var startNode = mapGenerator.GeneratedMap["Node_0_0"];
        MoveToNode(startNode);
    }

    public void TrySelectNode(string targetNodeID)
    {
        var mapData = mapGenerator.GeneratedMap;
        if (!mapData.ContainsKey(targetNodeID)) return;
        MapNodeData targetData = mapData[targetNodeID];

        // --- 👇【关键修复 A：路径合法性检查】 ---
        // 1. 状态必须是 Selectable (这个状态现在只在上一关打完后，给直连的节点点亮)
        if (targetData.NodeState != MapNodeState.Selectable) return;

        // 2. 强校验：目标节点必须是当前所在节点的“后继节点”
        // 处理第0层的特殊情况（CurrentNodeID 为空时）
        if (!string.IsNullOrEmpty(CurrentNodeID))
        {
            MapNodeData currentNode = mapData[CurrentNodeID];
            if (!currentNode.NextNodeIDs.Contains(targetNodeID))
            {
                Debug.LogWarning("【路径非法】只能进入当前位置直连的下一个房间！");
                return;
            }
        }
        // ------------------------------------------

        // 获取真实的判定类型 (如果是问号，则读取真实的内核)
        targetData.IsRevealed = true;

        // 2. 获取真实内核
        MapNodeType activeType = (targetData.NodeType == MapNodeType.Unknown)
                                 ? targetData.HiddenRealType
                                 : targetData.NodeType;

        // 3. 路由分发 (保持不变)
        if (IsCombatNode(activeType))
        {
            if (MapUIPanel != null) MapUIPanel.SetActive(false);
            CombatDirector.Instance.EnterCombatPhase(targetData);
        }
        else if (activeType == MapNodeType.Event)
        {
            if (MapUIPanel != null) MapUIPanel.SetActive(false);
            EventDirector.Instance.EnterEventPhase(targetData);
        }
        else if (activeType == MapNodeType.Workshop)
        {
            if (MapUIPanel != null) MapUIPanel.SetActive(false);
            ShopDirector.Instance.EnterShopPhase(targetData);
        }
    }

    // 👇【新增辅助方法】：判断这个类型是不是“打架”的节点
    private bool IsCombatNode(MapNodeType type)
    {
        return type == MapNodeType.Enemy_Tech ||
               type == MapNodeType.Enemy_Flesh ||
               type == MapNodeType.Enemy_Magic ||
               type == MapNodeType.Enemy_Mixed ||
               type == MapNodeType.Elite ||
               type == MapNodeType.Boss;
    }

    // 👇【新增】：打赢了回来，继续地图结算

    public void OnCombatVictory(MapNodeData nodeData)
    {
        if (MapUIPanel != null) MapUIPanel.SetActive(true);

        if (nodeData != null)
        {
            // 只有这里调用了 MoveToNode，节点状态才会变灰，前路才会亮起
            MoveToNode(nodeData);
        }

        MapVisualizer visualizer = MapUIPanel.GetComponentInChildren<MapVisualizer>(true);
        if (visualizer != null) visualizer.RefreshAllVisuals();
    }
    // 玩家实际到达该节点后的逻辑处理
    // 玩家实际到达该节点后的逻辑处理
    private void MoveToNode(MapNodeData newNode)
    {
        // 1. 把刚才站的节点状态设为“已通过”
        if (newNode == null) return; // 👈 终极保底，防止 line 154 崩溃

        if (!string.IsNullOrEmpty(CurrentNodeID))
        {
            mapGenerator.GeneratedMap[CurrentNodeID].NodeState = MapNodeState.Passed;
        }

        // 2. 👇【核心修复】：在玩家踏上新节点的那一刻，无情地锁死同一层的所有其他选项！
        foreach (var node in mapGenerator.GeneratedMap.Values)
        {
            // 如果节点和玩家新选的节点在同一层，且不是玩家选的这个，直接宣判死刑 (Locked)！
            if (node.LayerIndex == newNode.LayerIndex && node.NodeID != newNode.NodeID)
            {
                node.NodeState = MapNodeState.Locked;
            }
        }

        // 3. 更新玩家当前位置
        CurrentNodeID = newNode.NodeID;
        CurrentLayer = newNode.LayerIndex;
        newNode.NodeState = MapNodeState.Passed; // 自己踩上去就变为已通过

        // 4. 点亮它前方的所有连线节点，设为“可选择”
        foreach (string nextID in newNode.NextNodeIDs)
        {
            mapGenerator.GeneratedMap[nextID].NodeState = MapNodeState.Selectable;
        }

        Debug.Log($"【状态更新】已到达第 {CurrentLayer} 层。前方有 {newNode.NextNodeIDs.Count} 条路线可供选择！同层其他路线已截断！");
        if (GlobalResourceManager.Instance != null)
        {
            GlobalResourceManager.Instance.AdvanceDay();
        }
    }

     //🗑️ 注意：原来那个单独的 private void LockUnselectedSiblings() 方法可以彻底删掉了！

    private void LockUnselectedSiblings()
    {
        // 遍历所有节点，如果它和我在同一层，但不是我刚才选的那个，就把它彻底锁死
        foreach (var node in mapGenerator.GeneratedMap.Values)
        {
            if (node.LayerIndex == CurrentLayer && node.NodeID != CurrentNodeID)
            {
                node.NodeState = MapNodeState.Locked;
            }
        }
    }


}