using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventDirector : MonoBehaviour
{
    public static EventDirector Instance;

    [Header("=== 全局动态事件池 ===")]
    [Tooltip("把所有配好的 EventPoolConfigSO 拖进这里！")]
    public List<EventPoolConfigSO> GlobalEventPools = new List<EventPoolConfigSO>();

    [Header("=== UI 引用绑定 ===")]
    public GameObject EventPanel;
    public TMP_Text TitleText;
    public TMP_Text DescriptionText;
    public Image IllustrationImage;

    public Transform OptionsRoot;
    public GameObject OptionButtonPrefab;

    private MapNodeData currentNodeData;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        EventPanel.SetActive(false);
    }

    // ==========================================
    // 核心算法：四维环境解析与事件抽取
    // ==========================================
    public void EnterEventPhase(MapNodeData nodeData)
    {
        currentNodeData = nodeData;

        int currentStage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
        int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        // 1. 过滤：找到所有符合当前阶段和层数的事件池
        var validPools = GlobalEventPools.Where(p =>
            p.TargetStage == currentStage &&
            currentLayer >= p.MinDepth &&
            currentLayer <= p.MaxDepth &&
            p.Events.Count > 0 // 确保池子里得有事件
        ).ToList();

        if (validPools.Count == 0)
        {
            Debug.LogWarning($"【事件池警告】找不到匹配 Stage:{currentStage} | Layer:{currentLayer} 的事件池！直接返回大地图。");
            ExecuteReturnToMap();
            return;
        }

        // 2. 权重抽卡：挑出一个天选之池
        var selectedPool = PickPoolByWeight(validPools);

        // 3. 从这个池子里随机抽一个具体的文字事件
        EventNodeSO chosenEvent = selectedPool.Events[Random.Range(0, selectedPool.Events.Count)];

        Debug.Log($"<color=#00FFFF>【事件触发】</color> 成功从 [{selectedPool.name}] 池中抽取事件：[{chosenEvent.EventTitle}]");
        PlayEvent(chosenEvent);
    }

    private EventPoolConfigSO PickPoolByWeight(List<EventPoolConfigSO> pools)
    {
        float totalWeight = pools.Sum(p => p.PoolWeight);
        float roll = Random.Range(0, totalWeight);
        foreach (var pool in pools)
        {
            if (roll < pool.PoolWeight) return pool;
            roll -= pool.PoolWeight;
        }
        return pools.Last();
    }

    // ==========================================
    // UI 渲染与 ECA 拦截
    // ==========================================
    public void PlayEvent(EventNodeSO eventNode)
    {
        EventPanel.SetActive(true);

        TitleText.text = eventNode.EventTitle;
        DescriptionText.text = eventNode.EventDescription;

        if (eventNode.EventIllustration != null)
        {
            IllustrationImage.sprite = eventNode.EventIllustration;
            IllustrationImage.gameObject.SetActive(true);
        }
        else
        {
            IllustrationImage.gameObject.SetActive(false);
        }

        foreach (Transform child in OptionsRoot) Destroy(child.gameObject);

        foreach (var option in eventNode.Options)
        {
            GameObject btnObj = Instantiate(OptionButtonPrefab, OptionsRoot);
            Button btn = btnObj.GetComponent<Button>();

            TMP_Text[] texts = btnObj.GetComponentsInChildren<TMP_Text>();
            TMP_Text titleTxt = texts[0];
            TMP_Text flavorTxt = texts.Length > 1 ? texts[1] : null;
            TMP_Text failTxt = texts.Length > 2 ? texts[2] : null;

            titleTxt.text = option.OptionText;
            if (flavorTxt != null) flavorTxt.text = option.FlavorText;

            // ECA Condition 判定：灰显与红字警告
            bool canAfford = true;
            string finalFailReason = "";

            foreach (var condition in option.Conditions)
            {
                if (condition != null && !condition.Evaluate(out string failReason))
                {
                    canAfford = false;
                    finalFailReason += failReason + "\n";
                }
            }

            if (canAfford)
            {
                btn.interactable = true;
                if (failTxt != null) failTxt.text = "";

                btn.onClick.AddListener(() => OnOptionClicked(option));
            }
            else
            {
                btn.interactable = false;
                if (failTxt != null) failTxt.text = $"<color=#FF0000>{finalFailReason}</color>";
            }
        }
    }

    private void OnOptionClicked(EventOption option)
    {
        foreach (var action in option.Actions)
        {
            if (action != null) action.Execute();
        }

        if (option.NextEventNode != null)
        {
            PlayEvent(option.NextEventNode);
        }
        else
        {
            bool triggeredLoot = option.Actions.Exists(a => a is EventAction_GrantLoot);
            EventPanel.SetActive(false);

            if (!triggeredLoot)
            {
                ExecuteReturnToMap();
            }
        }
    }

    public void ExecuteReturnToMap()
    {
        Debug.Log("【事件导演】事件结束，系统交接给大地图...");
        MapManager.Instance.OnCombatVictory(currentNodeData);
    }
}