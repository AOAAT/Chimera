using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComponentRefitUI : MonoBehaviour
{
    public static ComponentRefitUI Instance;

    [Header("=== 目标组件信息 ===")]
    public Image TargetIcon;
    public TMP_Text TargetNameText;
    public RectTransform SocketListRoot;
    public GameObject SocketSlotPrefab;
    [Header("=== 右侧配件库 ===")]
    public RectTransform AccessoryListRoot;
    public InventoryItemSlotUI AccessoryItemPrefab;
    private InstancedComponent editingComponent;
    public Button CloseButton; // 👈 退出按钮
    private int currentSelectedSocketIndex = 0; // 当前选了哪个坑位
    private void Awake()
    {
        // 正常初始化 Instance
        if (Instance == null) Instance = this;
    }
    private void Start()
    {
        if (CloseButton != null) CloseButton.onClick.AddListener(ClosePanel);
    }

    public void OpenRefitPanel(InstancedComponent comp)
    {
        editingComponent = comp;

        // 1. 刷新基础视觉
        if (TargetIcon != null) TargetIcon.sprite = comp.BaseData.ComponentIcon;
        if (TargetNameText != null) TargetNameText.text = comp.BaseData.ComponentName;

        // 2. 执行核心刷新（生成插槽和仓库）
        RefreshUI();

        Debug.Log($"<color=cyan>【改装工作台】</color> 已载入零件：{comp.BaseData.ComponentName}");
    }
    public static void SafeOpen(InstancedComponent targetComp)
    {
        // 如果 Instance 为空，说明物体被禁用了，Awake 没跑
        if (Instance == null)
        {
            // 强行在场景中搜索（包括隐藏物体）
            Instance = FindObjectOfType<ComponentRefitUI>(true);
        }

        if (Instance != null)
        {
            // 先把自己激活，否则后续逻辑无法跑 Update 或协程
            Instance.gameObject.SetActive(true);
            Instance.OpenRefitPanel(targetComp);
        }
        else
        {
            Debug.LogError("<color=red>【UI严重错误】</color> 场景中找不到 ComponentRefitUI 物体，请确认是否误删或未挂载脚本！");
        }
    }
    public void RefreshUI()
    {
        // 1. 刷新左侧插槽 (根据零件最大孔位动态生成)
        foreach (Transform child in SocketListRoot) Destroy(child.gameObject);
        int currentMax = editingComponent.GetMaxSockets();
        for (int i = 0; i < editingComponent.BaseData.MaxSocketCount; i++)
        {
            var socketObj = Instantiate(SocketSlotPrefab, SocketListRoot);
            var script = socketObj.GetComponent<SocketSlotUI>();

            // 查一下第 i 个坑位有没有装东西
            InstancedAccessory pluggedChip = null;
            if (i < editingComponent.SocketedAccessoryIDs.Count)
            {
                pluggedChip = PlayerInventoryManager.Instance.GetAccessoryInstance(editingComponent.SocketedAccessoryIDs[i]);
            }

            script.Initialize(i, pluggedChip, i == currentSelectedSocketIndex, (idx) => {
                currentSelectedSocketIndex = idx;
                RefreshUI(); // 切换选中的孔，重刷
            });
        }

        // 2. 刷新右侧仓库 (带契约审计)
        foreach (Transform child in AccessoryListRoot) Destroy(child.gameObject);

        // A. 强制首位：卸载按钮 (图3需求)
        var unequipBtn = Instantiate(AccessoryItemPrefab, AccessoryListRoot);
        unequipBtn.SetupUnequip(() => OnAccessoryClicked(null));

        // B. 遍历显示所有芯片
        foreach (var chip in PlayerInventoryManager.Instance.AccessoryInventory)
        {
            var slot = Instantiate(AccessoryItemPrefab, AccessoryListRoot);

            // 契约审计：这块芯片能不能装在这个零件上？
            string failReason;
            bool canFit = AccessoryValidator.CanFitAccessory(editingComponent, chip.BaseData, out failReason);

            // 如果芯片已经被别人装了，标记它
            string occupantName = "";
            if (chip.IsEquipped)
            {
                var otherComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == chip.ParentComponentID);
                occupantName = otherComp != null ? otherComp.BaseData.ComponentName : "未知零件";
            }

            // 🌟 我们需要去 InventoryItemSlotUI 补一个适配配件的 Setup 方法
            slot.SetupAccessoryRefitMode(chip, canFit, failReason, occupantName, OnAccessoryClicked);
        }
    }

    private void OnAccessoryClicked(InstancedAccessory chip)
    {
        if (chip == null) // 卸载逻辑
        {
            if (currentSelectedSocketIndex < editingComponent.SocketedAccessoryIDs.Count)
            {
                string chipID = editingComponent.SocketedAccessoryIDs[currentSelectedSocketIndex];
                PlayerInventoryManager.Instance.UnequipAccessoryFromComponent(chipID, editingComponent);
            }
        }
        else // 安装逻辑
        {
            string error;
            // 尝试注入 (EquipAccessoryToComponent 内部会运行 Validator)
            if (PlayerInventoryManager.Instance.EquipAccessoryToComponent(chip.InstanceID, editingComponent, out error))
            {
                GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Attach);
            }
            else
            {
                Debug.LogWarning($"【安装失败】{error}");
            }
        }
        RefreshUI();
    }
    public void ClosePanel()
    {
        gameObject.SetActive(false);
        // 关闭时刷新一下主仓库和机库，保证“插槽小点”同步更新
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
    }
}