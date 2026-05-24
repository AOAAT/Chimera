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

    [Header("=== 终局设定 ===")]
    [Tooltip("当玩家踩到 BOSS 节点时，触发的特定终章事件")]
    public EventNodeSO FinaleEvent;

    // 您大纲里提到的全局资源，未来会接管这部分
    //public int CurrentSAN = 100;
    //public int CurrentPower = 50;

    private void Awake()
    {
        // 修改点：去掉了 DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

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
        MusicManager.Instance?.SwitchState(MusicState.Map);
        // 找到第0层的起点，把玩家扔上去
        var startNode = mapGenerator.GeneratedMap["Node_0_0"];
        MoveToNode(startNode);
    }

    public void TrySelectNode(string targetNodeID)
    {
        var mapData = mapGenerator.GeneratedMap;
        if (!mapData.ContainsKey(targetNodeID)) return;
        MapNodeData targetData = mapData[targetNodeID];

        // --- 1. 路径合法性检查 (Path Validation) ---
        // A. 状态检查：必须是已经亮起的“可选择”节点
        if (targetData.NodeState != MapNodeState.Selectable) return;

        // B. 连通性检查：目标节点必须是当前节点的直连后继
        if (!string.IsNullOrEmpty(CurrentNodeID))
        {
            MapNodeData currentNode = mapData[CurrentNodeID];
            if (!currentNode.NextNodeIDs.Contains(targetNodeID))
            {
                Debug.LogWarning("<color=red>【路径非法】</color> 指挥官，探测到尝试跳过隔离区的行为，指令已拦截。");
                return;
            }
        }

        // --- 2. 状态揭示与类型锁定 ---
        // 踏入瞬间，强制探明真相 (处理问号房)
        targetData.IsRevealed = true;

        // 获取内核真实类型
        MapNodeType activeType = (targetData.NodeType == MapNodeType.Unknown)
                                 ? targetData.HiddenRealType
                                 : targetData.NodeType;

        // --- 3. 核心路由分发逻辑 (Routing) ---

        // 👇【核心重定向】：BOSS 节点叙事劫持
        if (activeType == MapNodeType.Boss)
        {
            if (FinaleEvent != null)
            {
                // 关闭大地图卷轴 UI
                if (MapUIPanel != null) MapUIPanel.SetActive(false);

                // 播放预设的终章文字事件 (您可以在这里说想说的话)
                EventDirector.Instance.PlayEvent(FinaleEvent);

                Debug.Log("<color=gold>【核心协议】</color> 侦测到节点 15 (BOSS)，正在载入终章叙事模块...");
            }
            else
            {
                Debug.LogError("【系统异常】已抵达终局节点，但 FinaleEvent 槽位为空！请在 MapManager 检查配置。");
            }
            return; // 核心拦截，防止进入后续的战斗逻辑
        }

        if (IsCombatNode(activeType))
        {
            Debug.Log($"<color=orange>【RTS 路由】</color> 踏入冲突区域 {targetNodeID}，目前直接判定为路过。");

            // 直接执行胜利后的回归逻辑（即：直接站上去，亮起后面的路）
            OnCombatVictory(targetData);
        }
        // B. 文字事件节点
        else if (activeType == MapNodeType.Event)
        {
            if (MapUIPanel != null) MapUIPanel.SetActive(false);
            EventDirector.Instance.EnterEventPhase(targetData);
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

    }
    public void OnCombatVictory(MapNodeData nodeData)
    {
        // 这个方法现在只负责两件事：显示地图、切换音乐
        if (MapUIPanel != null) MapUIPanel.SetActive(true);
        MusicManager.Instance?.SwitchState(MusicState.Map);

        // 如果传入了节点，执行移动逻辑（亮起后续路线）
        if (nodeData != null)
        {
            MoveToNode(nodeData);
        }

        MapVisualizer visualizer = MapUIPanel.GetComponentInChildren<MapVisualizer>(true);
        if (visualizer != null) visualizer.RefreshAllVisuals();
    }
}