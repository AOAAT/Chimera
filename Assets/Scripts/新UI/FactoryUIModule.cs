using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class FactoryUIModule : MonoBehaviour
{
    [Header("=== UI 引用 ===")]
    public Transform ShelfGrid;      // 上方货架 (Grid Layout Group)
    public GameObject ShelfSlotPrefab; // 货架格子预制体
    public RectTransform DetailAnchor; // 你在工厂 UI 里选定的详情页固定位置

    public void Initialize()
    {
        // 默认显示底盘列表
        ShowChassisShelf();
    }

    // --- 按钮点击事件：切换到底盘 ---
    public void ShowChassisShelf()
    {
        ClearShelf();
        foreach (var data in PlayerInventoryManager.Instance.AllChassisDatabase)
        {
            CreateSlot(data.ChassisSprite, data.ChassisName, () => {
                PlayerInventoryManager.Instance.AddChassisToWarehouse(data, 1);
            }, () => ItemDetailPanelUI.Instance.ShowChassisDetail(data));
        }
    }

    // --- 按钮点击事件：切换到组件 ---
    public void ShowComponentShelf()
    {
        ClearShelf();
        foreach (var data in PlayerInventoryManager.Instance.AllComponentDatabase)
        {
            CreateSlot(data.ComponentIcon, data.ComponentName, () => {
                PlayerInventoryManager.Instance.AddComponentToWarehouse(data, 1, 1);
            }, () => ItemDetailPanelUI.Instance.ShowComponentDetail(new InstancedComponent(data, 1)));
        }
    }

    private void CreateSlot(Sprite icon, string itemName, System.Action onClick, System.Action onHover)
    {
        GameObject slotObj = Instantiate(ShelfSlotPrefab, ShelfGrid);

        // 1. 设置图标 (核心：复用大图并保持比例)
        Image img = slotObj.transform.Find("Icon").GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true; // 🌟 必须勾选，防止大图拉伸

        // 2. 绑定生产逻辑 (瞬发验证)
        slotObj.GetComponent<Button>().onClick.AddListener(() => {
            onClick.Invoke();
            // 可以在这里加个简单的“叮”一声反馈
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ItemEject);
        });

        // 3. 🌟 核心：详情页重定向
        var trigger = slotObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // 悬停进入：告诉详情页锁死在我的锚点上
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((e) => {
            ItemDetailPanelUI.Instance.SetFixedAnchor(DetailAnchor);
            onHover.Invoke();
        });
        trigger.triggers.Add(enter);

        // 鼠标移出：可以清空详情，或者保持最后一次显示
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener((e) => ItemDetailPanelUI.Instance.HidePanel());
        trigger.triggers.Add(exit);
    }

    private void ClearShelf()
    {
        foreach (Transform child in ShelfGrid) Destroy(child.gameObject);
    }
}