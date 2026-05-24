using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventDirector : MonoBehaviour
{
    public static EventDirector Instance;

    [Header("=== 全局动态事件池 ===")]
    public List<EventPoolConfigSO> GlobalEventPools = new List<EventPoolConfigSO>();

    [Header("=== UI 引用绑定 ===")]
    public GameObject EventPanel;
    public TMP_Text TitleText;
    public TMP_Text DescriptionText;
    public Image IllustrationImage;
    public Transform OptionsRoot;
    public GameObject OptionButtonPrefab;
    public MapNodeData CurrentNodeData => currentNodeData;
    private MapNodeData currentNodeData;
    private EventNodeSO currentEventNode;
    private List<EventOptionUI> activeOptionUIs = new List<EventOptionUI>();


    private static EventNodeSO nextNodeAfterLoot;

    public static EventNodeSO GetPendingNextNode() => nextNodeAfterLoot;
    public static void ClearPendingNextNode() => nextNodeAfterLoot = null;


    private void Awake() { Instance = this; }

    private void Start()
    {
        if (EventPanel != null) EventPanel.SetActive(false);

        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged += RefreshCurrentOptions;
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged += RefreshCurrentOptions;
    }

    private void OnDestroy()
    {
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged -= RefreshCurrentOptions;
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= RefreshCurrentOptions;
    }

    public void EnterEventPhase(MapNodeData nodeData)
    {
        currentNodeData = nodeData;

        // 👇【核心修改】：直接获取当前层数，不再去 RunManager 找 Stage
        int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        // 过滤掉不符合层数深度的池子（不再检查 TargetStage）
        var validPools = GlobalEventPools.Where(p =>
            currentLayer >= p.MinDepth && currentLayer <= p.MaxDepth && p.Events.Count > 0
        ).ToList();

        if (validPools.Count == 0) { ExecuteReturnToMap(); return; }

        var selectedPool = PickPoolByWeight(validPools);

        // 基础条件审查
        var qualifiedEvents = selectedPool.Events.Where(e =>
        {
            if (e == null) return false;
            if (e.AppearanceConditions == null || e.AppearanceConditions.Count == 0) return true;
            foreach (var cond in e.AppearanceConditions)
            {
                if (cond != null && !cond.Evaluate(out _)) return false;
            }
            return true;
        }).ToList();

        if (qualifiedEvents.Count == 0) { ExecuteReturnToMap(); return; }

        EventNodeSO chosenEvent = qualifiedEvents[Random.Range(0, qualifiedEvents.Count)];
        PlayEvent(chosenEvent);
    }

    private EventPoolConfigSO PickPoolByWeight(List<EventPoolConfigSO> pools)
    {
        float totalWeight = pools.Sum(p => p.PoolWeight);
        float roll = Random.Range(0f, totalWeight);
        foreach (var pool in pools)
        {
            if (roll < pool.PoolWeight) return pool;
            roll -= pool.PoolWeight;
        }
        return pools.Last();
    }

    public void PlayEvent(EventNodeSO eventNode)
    {
        // --- 👇【关键修复】：只要调用了 PlayEvent，就标记流程已被劫持 ---
        MusicManager.Instance?.PlayEventMusic(eventNode.CustomBGM);

        currentEventNode = eventNode;
        if (EventPanel != null) EventPanel.SetActive(true);
        if (TitleText != null) TitleText.text = eventNode.EventTitle;
        if (DescriptionText != null) DescriptionText.text = eventNode.EventDescription;

        if (IllustrationImage != null)
        {
            if (eventNode.EventIllustration != null)
            {
                IllustrationImage.sprite = eventNode.EventIllustration;
                IllustrationImage.gameObject.SetActive(true);
            }
            else IllustrationImage.gameObject.SetActive(false);
        }

        foreach (Transform child in OptionsRoot) Destroy(child.gameObject);
        activeOptionUIs.Clear();

        foreach (var option in eventNode.Options)
        {
            if (option == null) continue;
            GameObject btnObj = Instantiate(OptionButtonPrefab, OptionsRoot);
            EventOptionUI optionUI = btnObj.GetComponent<EventOptionUI>();
            EventOption capturedOption = option;
            optionUI.Initialize(capturedOption.OptionText, capturedOption.FlavorText, () => OnOptionClicked(capturedOption));
            activeOptionUIs.Add(optionUI);
        }
        RefreshCurrentOptions();
    }

    public void RefreshCurrentOptions()
    {
        if (EventPanel == null || !EventPanel.activeSelf || currentEventNode == null || activeOptionUIs.Count == 0) return;
        for (int i = 0; i < currentEventNode.Options.Count; i++)
        {
            if (i >= activeOptionUIs.Count) break;
            var option = currentEventNode.Options[i];
            var ui = activeOptionUIs[i];
            bool canAfford = true;
            string finalFailReason = "";

            foreach (var condition in option.Conditions)
            {
                if (condition != null && !condition.Evaluate(out string failReason))
                {
                    canAfford = false;
                    finalFailReason += failReason + "  ";
                }
            }
            ui.UpdateState(canAfford, canAfford ? "" : finalFailReason.TrimEnd(' '));
        }
    }


    private void OnOptionClicked(EventOption option)
    {
        if (option == null) return;

        // 1. 注册可能存在的接力节点
        nextNodeAfterLoot = option.NextEventNode;

        // 2. 👇【核心修复】：删除了对 TriggerSpecialCombat 的引用
        bool isFlowHijacked = option.Actions.Any(a =>
            a is EventAction_OpenSpecificUpgrade
        // 这里原本是检查特殊战斗的地方，现在已经被肃清
        );

        // 3. 执行所有积木动作
        foreach (var action in option.Actions) if (action != null) action.Execute();

        // ... 后续逻辑（流程分流）保持不变 ...
        if (isFlowHijacked)
        {
            if (EventPanel != null) EventPanel.SetActive(false);
        }
        else if (option.NextEventNode != null)
        {
            PlayEvent(option.NextEventNode);
        }
        else
        {
            if (EventPanel != null) EventPanel.SetActive(false);
            ExecuteReturnToMap();
        }
    }
    public void ExecuteReturnToMap()
    {
        // 既然 OnCombatVictory 被删了，我们直接开启地图面板并刷新视觉
        if (MapManager.Instance != null && MapManager.Instance.MapUIPanel != null)
        {
            MapManager.Instance.MapUIPanel.SetActive(true);

            // 尝试寻找地图视觉组件并重刷状态
            MapVisualizer viz = MapManager.Instance.MapUIPanel.GetComponentInChildren<MapVisualizer>(true);
            if (viz != null) viz.RefreshAllVisuals();
        }
    }
}