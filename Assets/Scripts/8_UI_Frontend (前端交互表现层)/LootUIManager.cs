using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LootUIManager : MonoBehaviour
{
    public static LootUIManager Instance;

    [Header("=== 阶段 0：集散大厅 (The Hub) ===")]
    public GameObject HubPanel;
    public Transform HubListRoot;
    public GameObject HubEntryPrefab;  // 大厅里的条目按钮预制体
    public Button LeaveHubButton;      // 全部拿完后离开的按钮

    [Header("=== 阶段 1：标签选择 (Tag Panel) ===")]
    public GameObject TagPanel;
    public Transform TagButtonRoot;
    public GameObject TagButtonPrefab;

    [Header("=== 阶段 2：物品获取 (Item Panel) ===")]
    public GameObject ItemPanel;
    public Transform ItemSlotRoot;
    public InventoryItemSlotUI ItemSlotPrefab;
    public Button ConfirmButton;       // 收下
    public Button SalvageButton;       // 粉碎
    public Button ReturnButton;        // 👇【新增】：返回大厅 (不改变状态)

    private List<ActiveLootTask> currentTasks;
    private ActiveLootTask activeTask; // 玩家当前正在交互的那个包裹
    private InstancedComponent selectedItem;
    private List<InventoryItemSlotUI> spawnedItemSlots = new List<InventoryItemSlotUI>();
    private System.Action onHubClosedCallback;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        LeaveHubButton.onClick.AddListener(OnLeaveHubClicked);
        ConfirmButton.onClick.AddListener(OnConfirmClicked);
        SalvageButton.onClick.AddListener(OnSalvageClicked);
        ReturnButton.onClick.AddListener(OnReturnClicked); // 绑定返回按钮

        CloseAllPanels();
    }

    private void CloseAllPanels()
    {
        HubPanel.SetActive(false);
        TagPanel.SetActive(false);
        ItemPanel.SetActive(false);
    }

    // ==========================================
    // 渲染总集散大厅
    // ==========================================
    public void OpenHub(List<ActiveLootTask> tasks, System.Action onClose = null)
    {
        currentTasks = tasks;
        onHubClosedCallback = onClose; // 记住是谁叫我开门的
        RefreshHubUI();
        HubPanel.SetActive(true);
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

            // 状态机 UI 渲染逻辑
            if (task.IsClaimed)
            {
                txt.text = "<s>[已处理] 资源已入库</s>";
                txt.color = Color.gray;
                btn.interactable = false;
            }
            else if (task.IsBoxOpened)
            {
                // 已经开箱了，或者是单选盲盒直接就绪了
                txt.text = "<color=#00FFFF>【已开封的残骸】点击查看</color>";
                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
            else
            {
                // 还没开箱的处女地
                if (task.Config.Mode == LootDropMode.PlayerDrivenFilter)
                    txt.text = "<color=#FFD700>【深度查找】选择打捞方向</color>";
                else
                    txt.text = "【未知的机械盲盒】点击开启";

                btn.onClick.AddListener(() => OnHubEntryClicked(task));
            }
        }

        // 如果全部搞定，离开按钮变绿！
        LeaveHubButton.interactable = allClaimed;
        LeaveHubButton.GetComponentInChildren<TMP_Text>().text = allClaimed ? "离开废墟" : "还有未处理的战利品";
    }

    // ==========================================
    // 处理大厅条目点击的“岔路口”
    // ==========================================
    private void OnHubEntryClicked(ActiveLootTask task)
    {
        activeTask = task;

        // 情景 A：这是“深度查找”，且还没选标签
        if (task.Config.Mode == LootDropMode.PlayerDrivenFilter && !task.IsBoxOpened && !task.LockedTag.HasValue)
        {
            HubPanel.SetActive(false);
            OpenTagPanel();
        }
        // 情景 B：单抽盲盒 / 或者已经选完标签锁死后的包裹
        else
        {
            if (!task.IsBoxOpened)
            {
                // 让导演后台 Roll 出装备并锁死！
                LootSequenceDirector.Instance.RollItemsForTask(task);
            }
            HubPanel.SetActive(false);
            OpenItemPanel();
        }
    }

    // ==========================================
    // 标签选择面板
    // ==========================================
    private void OpenTagPanel()
    {
        TagPanel.SetActive(true);
        foreach (Transform child in TagButtonRoot) Destroy(child.gameObject);

        var choices = LootSequenceDirector.Instance.GetTagChoicesForTask(activeTask, 3);

        foreach (var tag in choices)
        {
            GameObject btnObj = Instantiate(TagButtonPrefab, TagButtonRoot);
            btnObj.GetComponentInChildren<TMP_Text>().text = TranslateTag(tag);

            SubTag capturedTag = tag;
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                // 选定标签，锁死！
                activeTask.LockedTag = capturedTag;
                TagPanel.SetActive(false);

                // 顺畅过渡：让系统根据这个锁死的标签 Roll 装备，然后直接打开物品面板
                LootSequenceDirector.Instance.RollItemsForTask(activeTask);
                OpenItemPanel();
            });
        }
    }

    // ==========================================
    // 物品展示面板 (带有返回按钮)
    // ==========================================
    private void OpenItemPanel()
    {
        ItemPanel.SetActive(true);
        ConfirmButton.interactable = false;
        selectedItem = null;

        foreach (Transform child in ItemSlotRoot) Destroy(child.gameObject);
        spawnedItemSlots.Clear();

        // 此时，GeneratedItems 绝对不可能为空，因为它在 Open 之前一定被 Roll 过并锁死了
        var items = activeTask.GeneratedItems;

        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            var item = items[i];

            var slot = Instantiate(ItemSlotPrefab, ItemSlotRoot);
            slot.SetupComponent(item, (_) => OnItemSlotClicked(index, item));
            slot.SetHighlight(false);
            spawnedItemSlots.Add(slot);
        }
    }

    private void OnItemSlotClicked(int index, InstancedComponent item)
    {
        selectedItem = item;
        ConfirmButton.interactable = true;
        for (int i = 0; i < spawnedItemSlots.Count; i++) spawnedItemSlots[i].SetHighlight(i == index);
    }

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
        // 👇【核心修复】：把 currentlySelectedItem 统一改成 selectedItem
        if (selectedItem != null)
        {
            var blueprint = selectedItem.BaseData;
            var lvData = blueprint.GetLevelData(selectedItem.CurrentLevel);
            int scrapVal = lvData != null ? lvData.ScrapValue : 5; // 兜底给5块钱

            if (GlobalResourceManager.Instance != null)
            {
                GlobalResourceManager.Instance.ModifyMaterials(scrapVal);
            }

            Debug.Log($"【粉碎资源】残骸已粉碎，获得了 {scrapVal} 点废料！");
            ConcludeTask();
        }
        else
        {
            Debug.LogWarning("请先选择一个物品再粉碎！");
        }
    }

    // 👇【核心功能】：后悔药按钮！什么都不改，直接切回大厅！
    private void OnReturnClicked()
    {
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI(); // 回去重新渲染大厅
    }

    private void ConcludeTask()
    {
        activeTask.IsClaimed = true; // 标记处理完毕
        ItemPanel.SetActive(false);
        HubPanel.SetActive(true);
        RefreshHubUI(); // 回去重新渲染，此时这个条目会变灰打勾！
    }

    private void OnLeaveHubClicked()
    {
        CloseAllPanels();

        if (onHubClosedCallback != null)
        {
            onHubClosedCallback.Invoke(); // 通知叫门的人，我关门了！
        }
        else
        {
            // 兜底方案
            CombatDirector.Instance.ExecuteReturnToMap();
        }
    }
    private string TranslateTag(SubTag tag)
    {
        switch (tag) { case SubTag.Ballistic: return "实弹"; case SubTag.Mutation: return "突变"; default: return tag.ToString(); }
    }
}