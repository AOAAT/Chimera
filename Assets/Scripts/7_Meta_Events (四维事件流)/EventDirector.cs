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
    private EventNodeSO currentEventNode;
    private List<EventOptionUI> activeOptionUIs = new List<EventOptionUI>();

    private void Awake() { Instance = this; }

    private void Start()
    {
        EventPanel.SetActive(false);
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
        int currentStage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
        int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        var validPools = GlobalEventPools.Where(p =>
            p.TargetStage == currentStage && currentLayer >= p.MinDepth && currentLayer <= p.MaxDepth && p.Events.Count > 0
        ).ToList();

        if (validPools.Count == 0) { ExecuteReturnToMap(); return; }
        var selectedPool = validPools[Random.Range(0, validPools.Count)];
        PlayEvent(selectedPool.Events[Random.Range(0, selectedPool.Events.Count)]);
    }

    public void PlayEvent(EventNodeSO eventNode)
    {
        currentEventNode = eventNode;
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

        foreach (var option in eventNode.Options)
        {
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
        if (!EventPanel.activeSelf || currentEventNode == null || activeOptionUIs.Count == 0) return;
        for (int i = 0; i < currentEventNode.Options.Count; i++)
        {
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
        foreach (var action in option.Actions) if (action != null) action.Execute();

        if (option.NextEventNode != null)
        {
            PlayEvent(option.NextEventNode);
        }
        else
        {
            // --- 👇【关键修复】：检查是否触发了会导致 UI 跳转的“万能奖励”积木 ---
            bool triggeredLootHub = option.Actions.Any(a =>
                a is EventAction_UniversalGrant ug && ug.Mode == RewardType.RandomLootBox);

            EventPanel.SetActive(false);

            // 如果没开启大巴扎 UI，才立即返回地图
            if (!triggeredLootHub) ExecuteReturnToMap();
        }
    }

    public void ExecuteReturnToMap()
    {
        MapManager.Instance.OnCombatVictory(currentNodeData);
    }
}