// --- START OF FILE LootUIManager.cs ---
using System.Collections; // 必须引用
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LootUIManager : MonoBehaviour
{
    public static LootUIManager Instance;
    private System.Action onHubClosedCallback;

    [Header("=== 阶段 0：集散大厅 (The Hub) ===")]
    public GameObject HubPanel;
    public Transform HubListRoot;      // 挂载着 Vertical Layout Group 的 Content 节点
    public GameObject HubEntryPrefab;  // 大厅盲盒条目预制体
    public Button LeaveHubButton;

    [Header("=== 阶段 1：标签选择 (绝对坐标) ===")]
    public GameObject TagPanel;
    [Tooltip("拖入你随意摆放的 3 个标签按钮")]
    public Button[] FixedTagButtons;
    [Tooltip("拖入这 3 个按钮对应的文本框")]
    public TMP_Text[] FixedTagTexts;

    [Header("=== 阶段 2：物品获取 (绝对坐标) ===")]
    public GameObject ItemPanel;
    [Tooltip("拖入你随意摆放的 3 个 InventoryItemSlotUI 预制体实例！建议 Element 0 放最中间")]
    public InventoryItemSlotUI[] FixedItemSlots;

    [Header("交互按钮")]
    public Button ConfirmButton;       // 收下
    public Button SalvageButton;       // 粉碎
    public Button ReturnButton;        // 返回大厅

    [Header("=== 弹射动画配置 ===")]
    public float EjectDuration = 0.5f;   // 弹射总时长
    public float ItemStaggerDelay = 0.1f; // 每个零件弹出的间隔（制造时序感）
    public AnimationCurve EjectCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 缓动曲线

    private List<ActiveLootTask> currentTasks;
    private ActiveLootTask activeTask;
    private InstancedComponent selectedItem;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        LeaveHubButton.onClick.AddListener(OnLeaveHubClicked);
        ConfirmButton.onClick.AddListener(OnConfirmClicked);
        SalvageButton.onClick.AddListener(OnSalvageClicked);
        ReturnButton.onClick.AddListener(OnReturnClicked);

        // 👇【核心安全锁】：强行锁死这 3 个展示格子的右键菜单，绝对防作弊！
        foreach (var slot in FixedItemSlots)
        {
            if (slot != null) slot.IsLootMode = true;
        }

        CloseAllPanels();
    }

    private void CloseAllPanels()
    {
        HubPanel.SetActive(false);
        TagPanel.SetActive(false);
        ItemPanel.SetActive(false);
    }

    // ==========================================
    // 路由大厅：解析战利品种类
    // ==========================================
    public void OpenHub(List<ActiveLootTask> tasks, System.Action onClose = null)
    {
        currentTasks = tasks;
        onHubClosedCallback = onClose;
        RefreshHubUI();
        HubPanel.SetActive(true);
        MusicManager.Instance?.SetImmersionMode(true);
    }

    private void RefreshHubUI()
    {
        foreach (Transform child in HubListRoot) Destroy(child.gameObject);

        bool allClaimed = true;

        foreach (var task in currentTasks)
        {
            if (!task.IsClaimed) allClaimed = false;

            GameObject entryObj = Instantiate(HubEntryPrefab, HubListRoot);
            Button btn = entryObj.GetComponent<Button>();
            TMP_Text txt = entryObj.GetComponentInChildren<TMP_Text>();

            if (task.IsClaimed)
            {
                txt.text = "<s>[已处理] 资源已入库</s>";
                txt.color = Color.gray;
                btn.interactable = false;
            }
            else if (task.IsBoxOpened)
            {
                txt.text = "<color=#00FFFF>【已开封的残骸】点击查看</color>";
                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
            else
            {
                // 动态文本解析：告诉玩家这是什么类型的盲盒
                if (task.Config.Mode == LootDropMode.PlayerDrivenFilter)
                    txt.text = "<color=#FFD700>【深度打捞】自主选择流派标签</color>";
                else if (task.Config.Mode == LootDropMode.CustomPoolDrop)
                    txt.text = "<color=#FF8800>【首领遗产】极品固定掉落</color>";
                else
                    txt.text = "【未知的机械盲盒】点击开启";

                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
        }

        LeaveHubButton.interactable = allClaimed;
        LeaveHubButton.GetComponentInChildren<TMP_Text>().text = allClaimed ? "离开废墟" : "还有未处理的战利品";
    }

    private void OnHubEntryClicked(ActiveLootTask task)
    {
        activeTask = task;

        // 【智能路由】：如果需要选标签，且还没选过
        if (task.Config.Mode == LootDropMode.PlayerDrivenFilter && !task.IsBoxOpened && !task.LockedTag.HasValue)
        {
            HubPanel.SetActive(false);
            OpenTagPanel();
        }
        else // 其他情况（单抽/系统随机/固定池/已经选过标签），直接开盲盒！
        {
            if (!task.IsBoxOpened) LootSequenceDirector.Instance.RollItemsForTask(task);
            HubPanel.SetActive(false);
            OpenItemPanel();
            StartCoroutine(ProcessScanningAndOpen(task));
        }
    }

    private IEnumerator ProcessScanningAndOpen(ActiveLootTask task)
    {
        // 1. 如果已经开过了，跳过扫描
        if (task.IsBoxOpened)
        {
            HubPanel.SetActive(false);
            OpenItemPanel();
            yield break;
        }

        // 2. 伪装扫描：可以禁用交互，并让鼠标变成忙碌状态
        Debug.Log("【系统】正在解析机械残骸...");

        // 这里你可以触发一个全屏的微弱扫描线特效
        // ScreenEffectManager.Instance.TriggerFlash(new Color(0, 1, 1, 0.1f), 0.5f);

        yield return new WaitForSecondsRealtime(0.6f); // 停顿一下，制造期待感

        // 3. 正式开盲盒
        LootSequenceDirector.Instance.RollItemsForTask(task);
        HubPanel.SetActive(false);
        OpenItemPanel();
    }

    // ==========================================
    // 渲染标签选择面板
    // ==========================================
    private void OpenTagPanel()
    {
        TagPanel.SetActive(true);
        var choices = LootSequenceDirector.Instance.GetTagChoicesForTask(activeTask, FixedTagButtons.Length);

        for (int i = 0; i < FixedTagButtons.Length; i++)
        {
            if (i < choices.Count)
            {
                FixedTagButtons[i].gameObject.SetActive(true);
                FixedTagTexts[i].text = TranslateTag(choices[i]);

                SubTag capturedTag = choices[i];
                FixedTagButtons[i].onClick.RemoveAllListeners();
                FixedTagButtons[i].onClick.AddListener(() =>
                {
                    activeTask.LockedTag = capturedTag;
                    TagPanel.SetActive(false);
                    // 选完标签，当场 Roll 出装备，转入展示面板！
                    LootSequenceDirector.Instance.RollItemsForTask(activeTask);
                    OpenItemPanel();
                });
            }
            else
            {
                // 选项不够 3 个，自动隐藏多余的按钮
                FixedTagButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // 渲染终极物品展示面板
    // ==========================================
    private void OpenItemPanel()
    {
        ItemPanel.SetActive(true);
        ConfirmButton.interactable = false;
        SalvageButton.interactable = false;
        selectedItem = null;

        var items = activeTask.GeneratedItems;

        for (int i = 0; i < FixedItemSlots.Length; i++)
        {
            if (i < items.Count)
            {
                FixedItemSlots[i].gameObject.SetActive(true);
                FixedItemSlots[i].SetHighlight(false);

                // 初始化状态：缩放为0，放在屏幕中心
                FixedItemSlots[i].transform.localScale = Vector3.zero;

                int index = i;
                var item = items[i];
                FixedItemSlots[i].SetupComponent(item, (_) => OnItemSlotClicked(index, item));

                // 👇【核心】：启动弹射协程
                StartCoroutine(AnimateItemEject(FixedItemSlots[i].GetComponent<RectTransform>(), i));
            }
            else
            {
                FixedItemSlots[i].gameObject.SetActive(false);
            }
        }
    }



    private void OnItemSlotClicked(int index, InstancedComponent item)
    {
        selectedItem = item;
        ConfirmButton.interactable = true;
        SalvageButton.interactable = true;

        // 刷新所有槽位的高亮框（互斥单选）
        for (int i = 0; i < FixedItemSlots.Length; i++)
        {
            if (FixedItemSlots[i].gameObject.activeSelf)
            {
                FixedItemSlots[i].SetHighlight(i == index);
            }
        }
    }
    private IEnumerator AnimateItemEject(RectTransform rect, int index)
    {
        // 1. 等待时序间隔
        yield return new WaitForSecondsRealtime(index * ItemStaggerDelay);

        Vector2 finalPos = rect.anchoredPosition; // 记住你在编辑器里摆好的位置
        Vector2 startPos = Vector2.zero;          // 从面板中心（0,0）出发

        float elapsed = 0;

        // 播放发射音效
        if (GlobalAudioManager.Instance != null)
            // 找一个清脆的机械弹出声
           
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ItemEject);
            while (elapsed < EjectDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 即使卡肉时间，UI 也要动
                float t = elapsed / EjectDuration;

                // 缓动计算
                float curveT = EjectCurve.Evaluate(t);

                // 坐标插值
                rect.anchoredPosition = Vector2.LerpUnclamped(startPos, finalPos, curveT);

                // 缩放插值（从0.5倍爆出，最后带一点点回弹）
                float scale = Mathf.LerpUnclamped(0.5f, 1.0f, curveT);
                rect.localScale = Vector3.one * scale;

                // 旋转动画（喷射时转个360度）
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(180, 0, curveT));

                yield return null;
            }

        // 落地反馈：微弱的屏幕震动
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.05f, 0.1f);

        rect.anchoredPosition = finalPos;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
// ==========================================
// 核心交互：收下 / 粉碎 / 返回
// ==========================================
private void OnConfirmClicked()
    {
        if (selectedItem != null)
        {
            PlayerInventoryManager.Instance.ComponentInventory.Add(selectedItem);
            PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
            Debug.Log($"【打捞成功】获得了 Lv.{selectedItem.CurrentLevel} [{selectedItem.BaseData.ComponentName}]");
            ConcludeTask();
        }
    }

    private void OnSalvageClicked()
    {
        if (selectedItem != null)
        {
            var blueprint = selectedItem.BaseData;
            var lvData = blueprint.GetLevelData(selectedItem.CurrentLevel);
            int scrapVal = lvData != null ? lvData.ScrapValue : 5;

            if (GlobalResourceManager.Instance != null) GlobalResourceManager.Instance.ModifyMaterials(scrapVal);

            Debug.Log($"【粉碎资源】残骸已粉碎，获得了 {scrapVal} 点废料！");
            ConcludeTask();
        }
    }

    private void OnReturnClicked()
    {
        // 啥也不干，只是切回上一个面板
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI();
    }

    private void ConcludeTask()
    {
        activeTask.IsClaimed = true; // 状态机锁死：已处理完毕
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI();
    }

    private void OnLeaveHubClicked()
    {
        CloseAllPanels();

        // 🌟 确保在这里恢复正常音质
        MusicManager.Instance?.SetImmersionMode(false);

        if (onHubClosedCallback != null) onHubClosedCallback.Invoke();
    }

    private string TranslateTag(SubTag tag)
    {
        // 可以在这里扩展你的翻译字典
        switch (tag)
        {
            case SubTag.Ballistic: return "实弹武装";
            case SubTag.Energy: return "能量科技";
            case SubTag.Mutation: return "血肉突变";
            case SubTag.Economy: return "经济扩容";
            default: return tag.ToString();
        }
    }
}