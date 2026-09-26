using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

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
    private FactoryBuilding boundFactory;
    private TMP_Text productivityText;
    private Image nextLineProgressFill;
    private GameObject productivityCard;

    public void Initialize()
    {
        Initialize(SelectionContextHUD.Instance != null
            ? SelectionContextHUD.Instance.CurrentTargetBuilding as FactoryBuilding
            : null);
    }

    public void Initialize(FactoryBuilding factory)
    {
        BindFactory(factory);
        EnsureProductivityCard();
        RefreshProductivityCard();
        // 初始默认显示底盘分类
        ShowChassisShelf();
    }

    private void OnDestroy()
    {
        if (boundFactory != null) boundFactory.OnStaffChanged -= HandleStaffChanged;
    }

    private void BindFactory(FactoryBuilding factory)
    {
        if (boundFactory == factory) return;
        if (boundFactory != null) boundFactory.OnStaffChanged -= HandleStaffChanged;
        boundFactory = factory;
        if (boundFactory != null) boundFactory.OnStaffChanged += HandleStaffChanged;
    }

    private void HandleStaffChanged(BuildingBase building)
    {
        RefreshProductivityCard();
    }

    // 2. 修改 Update 逻辑
    private void Update()
    {
        if (boundFactory == null && SelectionContextHUD.Instance != null)
            BindFactory(SelectionContextHUD.Instance.CurrentTargetBuilding as FactoryBuilding);

        FactoryBuilding factory = boundFactory;
        if (factory != null)
        {
            // 如果正在同步顺序，或者数量没变，不执行物理刷新（防止 Destroy 掉正在拖拽的物体）
            if (factory.SyncOrderFlag)
            {
                factory.SyncOrderFlag = false;
                lastTaskCount = factory.TaskQueue.Count; // 更新计数器
                return;
            }

            if (factory.TaskQueue.Count != lastTaskCount)
            {
                lastTaskCount = factory.TaskQueue.Count;
                RefreshQueueUI(factory);
            }
        }
    }

    private void EnsureProductivityCard()
    {
        if (productivityCard != null) return;

        productivityCard = new GameObject("产能概览", typeof(RectTransform), typeof(Image));
        productivityCard.transform.SetParent(transform, false);
        RectTransform cardRect = productivityCard.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(-246f, 5f);
        cardRect.sizeDelta = new Vector2(160f, 64f);

        Image cardImage = productivityCard.GetComponent<Image>();
        cardImage.sprite = Resources.Load<Sprite>("UI/ChimeraRounded");
        cardImage.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        cardImage.color = new Color(0.08f, 0.12f, 0.16f, 0.94f);

        GameObject textObject = new GameObject("产能文字", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(productivityCard.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 13f);
        textRect.offsetMax = new Vector2(-8f, -5f);
        productivityText = textObject.GetComponent<TextMeshProUGUI>();
        productivityText.alignment = TextAlignmentOptions.Center;
        productivityText.fontSize = 12f;
        productivityText.fontStyle = FontStyles.Bold;
        productivityText.color = Color.white;
        productivityText.lineSpacing = -5f;
        productivityText.raycastTarget = false;
        if (TaskItemPrefab != null)
        {
            TMP_Text sourceText = TaskItemPrefab.GetComponentInChildren<TMP_Text>(true);
            if (sourceText != null) productivityText.font = sourceText.font;
        }

        GameObject progressBackground = new GameObject("下一生产线进度", typeof(RectTransform), typeof(Image));
        progressBackground.transform.SetParent(productivityCard.transform, false);
        RectTransform backgroundRect = progressBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0f);
        backgroundRect.anchorMax = new Vector2(1f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = new Vector2(0f, 5f);
        backgroundRect.sizeDelta = new Vector2(-12f, 5f);
        Image backgroundImage = progressBackground.GetComponent<Image>();
        backgroundImage.sprite = cardImage.sprite;
        backgroundImage.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backgroundImage.color = new Color(1f, 1f, 1f, 0.12f);
        backgroundImage.raycastTarget = false;

        GameObject progressFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        progressFill.transform.SetParent(progressBackground.transform, false);
        RectTransform fillRect = progressFill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        nextLineProgressFill = progressFill.GetComponent<Image>();
        nextLineProgressFill.sprite = cardImage.sprite;
        nextLineProgressFill.type = Image.Type.Filled;
        nextLineProgressFill.fillMethod = Image.FillMethod.Horizontal;
        nextLineProgressFill.color = ChimeraUITheme.Accent;
        nextLineProgressFill.raycastTarget = false;

        UIHoverHint.Set(productivityCard, "居民生产力会同时提高生产速度；达到阈值后会开启额外的并行生产线。");
        productivityCard.transform.SetAsLastSibling();
    }

    private void RefreshProductivityCard()
    {
        if (boundFactory == null || productivityText == null) return;

        int staffCount = boundFactory.GetStaffList().Count;
        float productivity = boundFactory.TotalStaffProductivity;
        int lines = boundFactory.ActiveProductionLineCount;
        string nextLine = boundFactory.HasMaximumProductionLines
            ? "生产线已满"
            : $"距下条线 {boundFactory.ProductivityUntilNextLine:0.00}";
        productivityText.text = $"员工 {staffCount}/{boundFactory.MaxStaffCapacity} · 产能 {productivity:0.00}\n" +
            $"并行 {lines}/{boundFactory.MaxProductionLines} · 速度 {boundFactory.ProductionSpeedMultiplier:0.00}x\n" +
            nextLine;

        if (nextLineProgressFill != null)
            nextLineProgressFill.fillAmount = boundFactory.NextProductionLineProgress;

        string detail = boundFactory.HasMaximumProductionLines
            ? "并行生产线已经达到当前上限。"
            : $"距离下一条生产线还需要 {boundFactory.ProductivityUntilNextLine:0.00} 生产力。";
        UIHoverHint.Set(productivityCard,
            $"每 1 点生产力提高 {boundFactory.SpeedBonusPerProductivity:P0} 生产速度。{detail}");
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
            if (SelectionContextHUD.Instance.CurrentTargetBuilding is FactoryBuilding factory)
            {
                // --- 👇【核心修复逻辑】：提取成本数据 ---
                float time = 10f;
                ResourceSet cost = new ResourceSet(0, 0, 0); // 默认 0 成本兜底

                if (sourceSO is ComponentDataSO comp)
                {
                    time = comp.BaseProductionTime;
                    // 读取组件 Mk.1 型号的成本 (默认取第一项)
                    var modelData = comp.GetModelData(1);
                    if (modelData != null) cost = modelData.ProductionCost;
                }
                else if (sourceSO is ChassisDataSO chas)
                {
                    time = chas.BaseProductionTime;
                    // 读取底盘图纸上配置的成本
                    cost = chas.ProductionCost;
                }

                // --- 🌟 关键：现在传入 5 个参数，补全 cost ---
                factory.AddToQueue(sourceSO, itemName, icon, time, cost);
            }
        });

        // 3. 详情页重定向
        var trigger = slotObj.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? slotObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        ResourceSet previewCost = new ResourceSet();
        if (sourceSO is ComponentDataSO component) previewCost = component.GetModelData(1)?.ProductionCost ?? new ResourceSet();
        else if (sourceSO is ChassisDataSO chassis) previewCost = chassis.ProductionCost;

        // 鼠标进入
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((e) => {
            ItemDetailPanelUI.Instance.SetFixedAnchor(DetailAnchor);
            onHover.Invoke();
            string duration = boundFactory != null
                ? $"基础 {prodTime:0.#} 秒 · 当前 {boundFactory.GetEffectiveProductionTime(prodTime):0.#} 秒"
                : $"{prodTime:0.#} 秒";
            UIFeedback.Show($"{itemName} · 生产时间 {duration}\n成本：{UIFeedback.Cost(previewCost)}");
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
        // 1. 彻底清理旧格子
        foreach (Transform child in TaskQueueContainer) Destroy(child.gameObject);

        // 2. 重新生成
        foreach (var task in factory.TaskQueue)
        {
            GameObject itemObj = Instantiate(TaskItemPrefab, TaskQueueContainer);
            var itemScript = itemObj.GetComponent<ProductionTaskUIItem>();

            if (itemScript != null)
            {
                // --- 👇【关键修复点】：修改这里的回调逻辑 ---
                itemScript.Initialize(task, () => {

                    // 🌟 不要直接 Remove，而是调用 factory 封装好的 CancelTask 方法！
                    // 这样工厂才会执行 GlobalResourceManager.Instance.Refund(task.PaidCost);
                    factory.CancelTask(task);

                    // 然后再刷新 UI 表现
                    RefreshQueueUI(factory);
                });
            }
        }
    }

    private void ClearShelf()
    {
        foreach (Transform child in ShelfGrid) Destroy(child.gameObject);
    }
}
