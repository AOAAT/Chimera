// --- START OF FILE LootUIManager.cs ---
using System.Collections;
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

    // 👇【双类型缓存】：支持底盘和组件
    private InstancedComponent selectedItem;
    private InstancedChassis selectedChassis;

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
                txt.text = "<color=#00FFFF>【已搜寻的残骸】点击查看</color>";
                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
            else if (task.Config.Mode == LootDropMode.CustomPoolDrop)
            {
                // 👇【优化】：如果是特定奖励，优先显示底盘名，否则显示第一个零件名
                string rewardName = "固定掉落";
                if (task.GeneratedChassis.Count > 0)
                    rewardName = task.GeneratedChassis[0].BaseData.ChassisName;
                else if (task.GeneratedItems.Count > 0)
                    rewardName = task.GeneratedItems[0].BaseData.ComponentName;

                txt.text = $"<color=#FF8800>【特殊奖励】{rewardName}</color>";
                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
            else
            {
                // 动态文本解析
                if (task.Config.Mode == LootDropMode.PlayerDrivenFilter)
                    txt.text = "<color=#FFD700>【深度搜寻】自主选择流派标签</color>";
                else
                    txt.text = "【战场打捞】点击获取残骸";

                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
        }

        LeaveHubButton.interactable = allClaimed;
        LeaveHubButton.GetComponentInChildren<TMP_Text>().text = allClaimed ? "离开" : "还有未处理的战利品";
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
        else // 其他情况直接开盲盒
        {
            StartCoroutine(ProcessScanningAndOpen(task));
        }
    }

    private IEnumerator ProcessScanningAndOpen(ActiveLootTask task)
    {
        HubPanel.SetActive(false);

        if (!task.IsBoxOpened)
        {
            // 伪装扫描
            Debug.Log("【系统】正在解析战场残骸...");
            yield return new WaitForSecondsRealtime(0.6f);
            LootSequenceDirector.Instance.RollItemsForTask(task);
        }

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
                    LootSequenceDirector.Instance.RollItemsForTask(activeTask);
                    OpenItemPanel();
                });
            }
            else
            {
                FixedTagButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // 渲染终极物品展示面板 (支持混排)
    // ==========================================
    private void OpenItemPanel()
    {
        ItemPanel.SetActive(true);
        ConfirmButton.interactable = false;
        SalvageButton.interactable = false;
        selectedItem = null;
        selectedChassis = null;

        int slotIdx = 0;

        // 1. 优先摆放生成的底盘
        foreach (var chassis in activeTask.GeneratedChassis)
        {
            if (slotIdx >= FixedItemSlots.Length) break;

            var slot = FixedItemSlots[slotIdx];
            slot.gameObject.SetActive(true);

            // 👇【核心修改】：物理重置高亮状态
            slot.SetHighlight(false);

            slot.transform.localScale = Vector3.zero;

            int index = slotIdx;
            InstancedChassis capturedChassis = chassis;
            slot.SetupChassis(chassis, (_) => OnChassisSlotClicked(index, capturedChassis));

            StartCoroutine(AnimateItemEject(slot.GetComponent<RectTransform>(), slotIdx));
            slotIdx++;
        }

        // 2. 接着摆放生成的零件
        foreach (var item in activeTask.GeneratedItems)
        {
            if (slotIdx >= FixedItemSlots.Length) break;

            var slot = FixedItemSlots[slotIdx];
            slot.gameObject.SetActive(true);

            // 👇【核心修改】：物理重置高亮状态
            slot.SetHighlight(false);

            slot.transform.localScale = Vector3.zero;

            int index = slotIdx;
            InstancedComponent capturedItem = item;
            slot.SetupComponent(item, (_) => OnItemSlotClicked(index, capturedItem));

            StartCoroutine(AnimateItemEject(slot.GetComponent<RectTransform>(), slotIdx));
            slotIdx++;
        }

        // 隐藏剩下的空格
        for (int i = slotIdx; i < FixedItemSlots.Length; i++)
        {
            FixedItemSlots[i].gameObject.SetActive(false);
        }
    }
    private void OnItemSlotClicked(int index, InstancedComponent item)
    {
        // 逻辑赋值
        selectedItem = item;
        selectedChassis = null;
        ConfirmButton.interactable = true;

        // 播放点击反馈音效 (可选)
        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Generic_Click);

        UpdateSalvageButtonState();

        // 👇【执行高亮切换】
        RefreshHighlights(index);
    }

    private void OnChassisSlotClicked(int index, InstancedChassis chassis)
    {
        // 逻辑赋值
        selectedChassis = chassis;
        selectedItem = null;
        ConfirmButton.interactable = true;

        // 播放点击反馈音效 (可选)
        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Generic_Click);

        UpdateSalvageButtonState();

        // 👇【执行高亮切换】
        RefreshHighlights(index);
    }




    private void UpdateSalvageButtonState()
    {
        if (activeTask != null && activeTask.IsForceClaim)
        {
            SalvageButton.interactable = false;
            SalvageButton.GetComponentInChildren<TMP_Text>().text = "<color=red>禁止拆解</color>";
        }
        else
        {
            SalvageButton.interactable = true;
            SalvageButton.GetComponentInChildren<TMP_Text>().text = "拆解以获得废料";
        }
    }

    private void RefreshHighlights(int selectedIdx)
    {
        for (int i = 0; i < FixedItemSlots.Length; i++)
        {
            // 只有当前正在显示的格子才参与逻辑
            if (FixedItemSlots[i].gameObject.activeSelf)
            {
                // 如果 i 等于传进来的点击索引，则开启高亮，否则关闭
                FixedItemSlots[i].SetHighlight(i == selectedIdx);

                // 【进阶体验】：未选中的格子可以稍微变暗一点点 (可选)
                // CanvasGroup cg = FixedItemSlots[i].GetComponent<CanvasGroup>();
                // if(cg != null) cg.alpha = (i == selectedIdx) ? 1.0f : 0.6f;
            }
        }
    }

    private IEnumerator AnimateItemEject(RectTransform rect, int index)
    {
        yield return new WaitForSecondsRealtime(index * ItemStaggerDelay);

        Vector2 finalPos = rect.anchoredPosition;
        Vector2 startPos = Vector2.zero;

        float elapsed = 0;
        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Loot_ItemEject);

        while (elapsed < EjectDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EjectCurve.Evaluate(elapsed / EjectDuration);

            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, finalPos, t);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1.0f, t);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(180, 0, t));

            yield return null;
        }

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
        Sprite iconToFly = null;
        Vector3 startPos = Input.mousePosition;

        // 寻找当前选中的格子位置
        foreach (var slot in FixedItemSlots)
        {
            if (slot.gameObject.activeSelf && slot.HighlightFrame.activeSelf)
            {
                startPos = slot.transform.position;
                break;
            }
        }

        if (selectedChassis != null)
        {
            iconToFly = selectedChassis.BaseData.ChassisSprite;
            PlayerInventoryManager.Instance.ChassisInventory.Add(selectedChassis);
            Debug.Log($"【底盘入库】获得了 [{selectedChassis.BaseData.ChassisName}]");
            ConcludeTask();
        }
        else if (selectedItem != null)
        {
            iconToFly = selectedItem.BaseData.ComponentIcon;
            PlayerInventoryManager.Instance.ComponentInventory.Add(selectedItem);
            Debug.Log($"【零件入库】获得了 Lv.{selectedItem.CurrentLevel} [{selectedItem.BaseData.ComponentName}]");
            ConcludeTask();
        }

        if (iconToFly != null && JuicyLootEffectManager.Instance != null)
        {
            JuicyLootEffectManager.Instance.SpawnFlyEffect(iconToFly, startPos);
        }

        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
    }

    private void OnSalvageClicked()
    {
        int scrapVal = 5;
        Vector3 startPos = Input.mousePosition;

        foreach (var slot in FixedItemSlots)
        {
            if (slot.gameObject.activeSelf && slot.HighlightFrame.activeSelf)
            {
                startPos = slot.transform.position;
                break;
            }
        }

        if (selectedChassis != null)
        {
            scrapVal = selectedChassis.BaseData.ScrapValue;
        }
        else if (selectedItem != null)
        {
            var lvData = selectedItem.BaseData.GetLevelData(selectedItem.CurrentLevel);
            scrapVal = lvData != null ? lvData.ScrapValue : 5;
        }

        if (JuicyLootEffectManager.Instance != null)
        {
            JuicyLootEffectManager.Instance.SpawnScrapExplosion(startPos, scrapVal);
        }

        GlobalResourceManager.Instance?.ModifyMaterials(scrapVal);
        ConcludeTask();
    }

    private void OnReturnClicked()
    {
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI();
    }

    private void ConcludeTask()
    {
        activeTask.IsClaimed = true;
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI();
    }

    private void OnLeaveHubClicked()
    {
        CloseAllPanels();
        MusicManager.Instance?.SetImmersionMode(false);
        if (onHubClosedCallback != null) onHubClosedCallback.Invoke();
    }

    private string TranslateTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.StrongAcid: return "强酸";
            case SubTag.Melee: return "近战";
            case SubTag.Ranged: return "远程";
            case SubTag.Charge: return "冲撞";
            case SubTag.Heavy: return "重型";
            case SubTag.Armor: return "装甲";
            case SubTag.Devotion: return "奉献";
            case SubTag.Smash: return "强击";
            case SubTag.Knockback: return "冲力";
            case SubTag.Wasteland: return "废土";
            case SubTag.Industry: return "工业";
            case SubTag.Firearms: return "枪械";
            case SubTag.Laboratory: return "实验室";
            case SubTag.Reload: return "装填";
            case SubTag.Kinetic: return "动能";
            case SubTag.Plasma: return "等离子";
            case SubTag.Head: return "头颅";
            case SubTag.Organs: return "内脏";
            case SubTag.Limbs: return "四肢";
            case SubTag.Parasite: return "寄生";
            case SubTag.Pain: return "痛苦";
            case SubTag.Artifact: return "遗物";
            case SubTag.Otherworld: return "异界";
            case SubTag.Mana: return "魔力";
            case SubTag.Chaos: return "混沌";
            case SubTag.Order: return "秩序";
            default: return tag.ToString();
        }
    }
}