using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class FactoryUIModule : MonoBehaviour
{
    [Header("=== 上方：货架货架 (Selection) ===")]
    public Transform ShelfGrid;        // 挂载 Grid Layout Group 的货架根节点
    public GameObject ShelfSlotPrefab; // 货架格子预制体 (Icon + Button)

    [Header("=== 下方：任务队列 (Task Queue) ===")]
    public Transform TaskQueueContainer; // 挂载 Vertical Layout Group 的队列根节点
    public GameObject TaskItemPrefab;    // 任务条预制体 (Progress Bar + Name + Cancel)

    [Header("=== 详情页定位 ===")]
    public RectTransform DetailAnchor;   // 详情页固定锚点

    // 内部缓存，用于减少不必要的 UI 刷新
    private int lastTaskCount = -1;

    public void Initialize()
    {
        // 初始默认显示底盘分类
        ShowChassisShelf();
    }

    // 2. 修改 Update 逻辑
    private void Update()
    {
        if (MainBuildingHUD.Instance.CurrentTargetBuilding is FactoryBuilding factory)
        {
            // 🌟 只有当队列数量发生变化时，才触发重绘，节省性能
            if (factory.TaskQueue.Count != lastTaskCount)
            {
                lastTaskCount = factory.TaskQueue.Count;
                RefreshQueueUI(factory);
            }
        }
        else
        {
            // 如果没选中工厂，重置计数
            lastTaskCount = -1;
        }
    }

    // ==========================================
    // 📦 货架填充逻辑
    // ==========================================

    public void ShowChassisShelf()
    {
        ClearShelf();
        foreach (var data in PlayerInventoryManager.Instance.AllChassisDatabase)
        {
            // 传入：图标、名字、源数据、生产时间、悬停回调
            CreateSlot(data.ChassisSprite, data.ChassisName, data, data.BaseProductionTime, () => {
                ItemDetailPanelUI.Instance.ShowChassisDetail(data);
            });
        }
    }

    public void ShowComponentShelf()
    {
        ClearShelf();
        foreach (var data in PlayerInventoryManager.Instance.AllComponentDatabase)
        {
            CreateSlot(data.ComponentIcon, data.ComponentName, data, data.BaseProductionTime, () => {
                // 暂时造一个 InstancedComponent 给详情页看
                ItemDetailPanelUI.Instance.ShowComponentDetail(new InstancedComponent(data, 1));
            });
        }
    }

    private void CreateSlot(Sprite icon, string itemName, Object sourceSO, float prodTime, System.Action onHover)
    {
        GameObject slotObj = Instantiate(ShelfSlotPrefab, ShelfGrid);

        // 1. 查找并设置缩略图 (复用大图逻辑)
        Transform iconTrans = slotObj.transform.Find("Icon");
        if (iconTrans != null)
        {
            Image img = iconTrans.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true; // 🌟 核心：确保不拉伸
        }

        slotObj.GetComponent<Button>().onClick.AddListener(() => {
            if (MainBuildingHUD.Instance.CurrentTargetBuilding is FactoryBuilding factory)
            {
                // 🌟 关键：从 SO 里读取 BaseProductionTime
                float time = 10f; // 默认值
                if (sourceSO is ComponentDataSO comp) time = comp.BaseProductionTime;
                else if (sourceSO is ChassisDataSO chas) time = chas.BaseProductionTime;

                factory.AddToQueue(sourceSO, itemName, icon, time);
                Debug.Log($"任务已加入: {itemName} | 耗时: {time}s");
            }
        });

        // 3. 详情页重定向
        var trigger = slotObj.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? slotObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // 鼠标进入
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((e) => {
            ItemDetailPanelUI.Instance.SetFixedAnchor(DetailAnchor);
            onHover.Invoke();
        });
        trigger.triggers.Add(enter);

        // 鼠标移出
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener((e) => ItemDetailPanelUI.Instance.HidePanel());
        trigger.triggers.Add(exit);
    }

    // ==========================================
    // 📋 任务列表渲染
    // ==========================================

    private void RefreshQueueUI(FactoryBuilding factory)
    {
        // 暴力刷新：清空所有任务条并重新生成
        foreach (Transform child in TaskQueueContainer) Destroy(child.gameObject);

        foreach (var task in factory.TaskQueue)
        {
            GameObject itemObj = Instantiate(TaskItemPrefab, TaskQueueContainer);
            var itemScript = itemObj.GetComponent<ProductionTaskUIItem>();

            if (itemScript != null)
            {
                // 初始化条目，并传入“取消任务”后的回调（退钱、删数据、刷UI）
                itemScript.Initialize(task, () => {
                    factory.TaskQueue.Remove(task);
                    RefreshQueueUI(factory); // 递归刷新
                });
            }
        }
    }

    private void ClearShelf()
    {
        foreach (Transform child in ShelfGrid) Destroy(child.gameObject);
    }
}