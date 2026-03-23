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
    public int CurrentSAN = 100;
    public int CurrentPower = 50;

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
        if (!mapGenerator.GeneratedMap.ContainsKey(targetNodeID)) return;
        MapNodeData targetData = mapGenerator.GeneratedMap[targetNodeID];

        if (targetData.NodeState != MapNodeState.Selectable) return;

        // 👇【核心修改】：切入战斗流！
        if (targetData.NodeType == MapNodeType.Enemy || targetData.NodeType == MapNodeType.Elite || targetData.NodeType == MapNodeType.Boss)
        {
            // 1. 隐藏地图界面的皮囊
            if (MapUIPanel != null) MapUIPanel.SetActive(false);

            // 2. 呼叫战斗导演，接管比赛！
            CombatDirector.Instance.EnterCombatPhase(targetData);
        }
        else if (targetData.NodeType == MapNodeType.Event || targetData.NodeType == MapNodeType.Workshop)
        {
            // TODO: 类似地，呼叫 EventDirector 或 WorkshopDirector
            Debug.Log("【地图管控】进入和平节点，即将弹出事件面板...");
            MoveToNode(targetData); // 目前测试先直接踩上去
        }
    }

    // 👇【新增】：打赢了回来，继续地图结算
    public void OnCombatVictory(MapNodeData nodeData)
    {
        // 1. 重新显示地图 UI
        if (MapUIPanel != null) MapUIPanel.SetActive(true);

        // 2. 核心状态机结算（打勾，点亮下一层）
        MoveToNode(nodeData);

        // 3. 👇【核心修复】：不能在自己身上找，要去 UI 节点里找 MapVisualizer！
        MapVisualizer visualizer = null;
        if (MapUIPanel != null)
        {
            // true 代表即使它是隐藏的也能找到
            visualizer = MapUIPanel.GetComponentInChildren<MapVisualizer>(true);
        }

        // 兜底防呆：如果上面没找到，就全宇宙广播找一下
        if (visualizer == null)
        {
            visualizer = FindObjectOfType<MapVisualizer>(true);
        }

        // 找到后命令 UI 刷新颜色
        if (visualizer != null)
        {
            visualizer.RefreshAllVisuals();
            Debug.Log("【系统广播】地图视觉已刷新，请长官选择下一节点！");
        }
        else
        {
            Debug.LogError("【严重警报】长官，找不到 MapVisualizer，地图颜色可能无法更新！");
        }
    }

    // 玩家实际到达该节点后的逻辑处理
    private void MoveToNode(MapNodeData newNode)
    {
        // 1. 把刚才站的节点状态设为“已通过”
        if (!string.IsNullOrEmpty(CurrentNodeID))
        {
            mapGenerator.GeneratedMap[CurrentNodeID].NodeState = MapNodeState.Passed;
            // 把周围的其他未选节点全部锁死，这就叫肉鸽的一锤子买卖！
            LockUnselectedSiblings();
        }

        // 2. 更新玩家当前位置
        CurrentNodeID = newNode.NodeID;
        CurrentLayer = newNode.LayerIndex;
        newNode.NodeState = MapNodeState.Passed; // 自己踩上去就变为已通过

        // 3. 点亮它前方的所有连线节点，设为“可选择”
        foreach (string nextID in newNode.NextNodeIDs)
        {
            mapGenerator.GeneratedMap[nextID].NodeState = MapNodeState.Selectable;
        }

        // TODO: 通知 UI 系统刷新整个地图的画面
        Debug.Log($"【状态更新】已到达第 {CurrentLayer} 层。前方有 {newNode.NextNodeIDs.Count} 条路线可供选择！");
    }

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