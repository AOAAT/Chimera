// --- START OF FILE LootUIManager.cs ---
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LootUIManager : MonoBehaviour
{
    public static LootUIManager Instance;

    [Header("=== 阶段 1：标签选择面板 ===")]
    public GameObject TagPanel;
    public Transform TagButtonRoot;    // 挂载 HorizontalLayoutGroup
    public GameObject TagButtonPrefab; // 预制体 (带 Button 和 Text)

    [Header("=== 阶段 2：物品选择面板 ===")]
    public GameObject ItemPanel;
    public Transform ItemSlotRoot;     // 挂载 HorizontalLayoutGroup
    public InventoryItemSlotUI ItemSlotPrefab; // 直接复用我们做好的格子！
    public Button ConfirmButton;
    public Button SalvageButton;       // 粉碎放弃按钮

    // 异步控制阀门！
    private TaskCompletionSource<SubTag> tagTcs;
    private TaskCompletionSource<InstancedComponent> itemTcs;

    private InstancedComponent currentlySelectedItem;
    private List<InventoryItemSlotUI> spawnedItemSlots = new List<InventoryItemSlotUI>();

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        ConfirmButton.onClick.AddListener(OnConfirmClicked);
        SalvageButton.onClick.AddListener(OnSalvageClicked);
        TagPanel.SetActive(false);
        ItemPanel.SetActive(false);
    }

    // ==========================================
    // UI 入口 A：呼出标签选择 (返回一个 Task 供后台等待)
    // ==========================================
    public Task<SubTag> RequestTagSelection(List<SubTag> tags)
    {
        tagTcs = new TaskCompletionSource<SubTag>(); // 制造一个“路障”
        TagPanel.SetActive(true);

        // 清理旧按钮
        foreach (Transform child in TagButtonRoot) Destroy(child.gameObject);

        // 生成新按钮
        foreach (var tag in tags)
        {
            GameObject btnObj = Instantiate(TagButtonPrefab, TagButtonRoot);
            btnObj.GetComponentInChildren<TMP_Text>().text = TranslateTag(tag);

            SubTag capturedTag = tag; // 闭包捕获
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                TagPanel.SetActive(false);
                tagTcs.TrySetResult(capturedTag); // 玩家点击后，挪开“路障”！代码继续跑！
            });
        }

        return tagTcs.Task;
    }

    // ==========================================
    // UI 入口 B：呼出物品获取 (返回一个 Task 供后台等待)
    // ==========================================
    public Task<InstancedComponent> RequestItemSelection(List<InstancedComponent> items)
    {
        itemTcs = new TaskCompletionSource<InstancedComponent>(); // 制造路障
        ItemPanel.SetActive(true);
        ConfirmButton.interactable = false; // 还没选东西，不准点确认
        currentlySelectedItem = null;

        foreach (Transform child in ItemSlotRoot) Destroy(child.gameObject);
        spawnedItemSlots.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            int captureIndex = i;
            var item = items[i];

            var slot = Instantiate(ItemSlotPrefab, ItemSlotRoot);
            slot.SetupComponent(item, (_) => OnItemSlotClicked(captureIndex, item));
            slot.SetHighlight(false);
            spawnedItemSlots.Add(slot);
        }

        return itemTcs.Task;
    }

    private void OnItemSlotClicked(int index, InstancedComponent item)
    {
        currentlySelectedItem = item;
        ConfirmButton.interactable = true;

        // UI 高亮排他
        for (int i = 0; i < spawnedItemSlots.Count; i++)
        {
            spawnedItemSlots[i].SetHighlight(i == index);
        }
    }

    private void OnConfirmClicked()
    {
        if (currentlySelectedItem != null)
        {
            ItemPanel.SetActive(false);
            itemTcs.TrySetResult(currentlySelectedItem); // 放行：玩家拿走了这件装备
        }
    }

    private void OnSalvageClicked()
    {
        // 放弃拿取，直接粉碎为通用资源 (目前传 null 代表什么都没拿)
        ItemPanel.SetActive(false);
        itemTcs.TrySetResult(null); // 放行：玩家什么都没拿
    }

    private string TranslateTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.Ballistic: return "实弹武装";
            case SubTag.Mutation: return "血肉突变";
            case SubTag.Heavy: return "重型挂载";
            default: return tag.ToString();
        }
    }
}