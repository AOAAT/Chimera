// --- START OF FILE EventDirector.cs ---
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

    private MapNodeData currentNodeData;

    // 👇【新增】：记录当前的事件和生成的按钮，方便随时刷新它们
    private EventNodeSO currentEventNode;
    private List<EventOptionUI> activeOptionUIs = new List<EventOptionUI>();

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        EventPanel.SetActive(false);

        // 👇【核心新增】：装上顺风耳！监听废料、SAN值、甚至库存的变动
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged += RefreshCurrentOptions;

        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged += RefreshCurrentOptions;
    }

    private void OnDestroy()
    {
        // 记得摘下耳机，防止内存泄漏
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged -= RefreshCurrentOptions;

        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= RefreshCurrentOptions;
    }

    public void EnterEventPhase(MapNodeData nodeData)
    {
        currentNodeData = nodeData;

        int currentStage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
        int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        var validPools = GlobalEventPools.Where(p =>
            p.TargetStage == currentStage && currentLayer >= p.MinDepth && currentLayer <= p.MaxDepth && p.Events.Count > 0
        ).ToList();

        if (validPools.Count == 0) { ExecuteReturnToMap(); return; }

        var selectedPool = PickPoolByWeight(validPools);
        EventNodeSO chosenEvent = selectedPool.Events[Random.Range(0, selectedPool.Events.Count)];

        PlayEvent(chosenEvent);
    }

    private EventPoolConfigSO PickPoolByWeight(List<EventPoolConfigSO> pools)
    {
        float totalWeight = pools.Sum(p => p.PoolWeight);
        float roll = Random.Range(0, totalWeight);
        foreach (var pool in pools) { if (roll < pool.PoolWeight) return pool; roll -= pool.PoolWeight; }
        return pools.Last();
    }

    public void PlayEvent(EventNodeSO eventNode)
    {
        currentEventNode = eventNode; // 记下当前在看哪个事件
        EventPanel.SetActive(true);

        TitleText.text = eventNode.EventTitle;
        DescriptionText.text = eventNode.EventDescription;

        if (eventNode.EventIllustration != null)
        {
            IllustrationImage.sprite = eventNode.EventIllustration;
            IllustrationImage.gameObject.SetActive(true);
        }
        else IllustrationImage.gameObject.SetActive(false);

        foreach (Transform child in OptionsRoot) Destroy(child.gameObject);
        activeOptionUIs.Clear();

        // 1. 先把所有选项按钮造出来，绑定好文字和回调
        foreach (var option in eventNode.Options)
        {
            GameObject btnObj = Instantiate(OptionButtonPrefab, OptionsRoot);
            EventOptionUI optionUI = btnObj.GetComponent<EventOptionUI>();

            EventOption capturedOption = option;
            optionUI.Initialize(capturedOption.OptionText, capturedOption.FlavorText, () => OnOptionClicked(capturedOption));

            activeOptionUIs.Add(optionUI);
        }

        // 2. 统一刷一次状态（判定红字和置灰）
        RefreshCurrentOptions();
    }

    // ==========================================
    // 👇【核心新增】：动态刷新机制！
    // 只要系统发了广播，我就重新算一遍这几个选项够不够钱买！
    // ==========================================
    public void RefreshCurrentOptions()
    {
        // 如果面板没开着，或者根本没事件，直接无视
        if (!EventPanel.activeSelf || currentEventNode == null || activeOptionUIs.Count == 0) return;

        for (int i = 0; i < currentEventNode.Options.Count; i++)
        {
            if (i >= activeOptionUIs.Count) break;

            var option = currentEventNode.Options[i];
            var ui = activeOptionUIs[i];

            bool canAfford = true;
            string finalFailReason = "";

            // 重新判定一遍条件
            foreach (var condition in option.Conditions)
            {
                if (condition != null && !condition.Evaluate(out string failReason))
                {
                    canAfford = false;
                    finalFailReason += failReason + "  ";
                }
            }

            // 告诉 UI 刷新自己的红字和交互状态！
            ui.UpdateState(canAfford, canAfford ? "" : finalFailReason.TrimEnd(' '));
        }
    }

    private void OnOptionClicked(EventOption option)
    {
        foreach (var action in option.Actions) if (action != null) action.Execute();

        if (option.NextEventNode != null) PlayEvent(option.NextEventNode);
        else
        {
            bool triggeredLoot = option.Actions.Exists(a => a is EventAction_GrantLoot);
            EventPanel.SetActive(false);
            if (!triggeredLoot) ExecuteReturnToMap();
        }
    }

    public void ExecuteReturnToMap()
    {
        MapManager.Instance.OnCombatVictory(currentNodeData);
    }
}