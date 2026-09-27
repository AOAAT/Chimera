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

    // 内部缓存，用于减少不必要的 UI 刷新
    private int lastTaskCount = -1;
    private FactoryBuilding boundFactory;
    [SerializeField] private TMP_Text productivityText;
    [SerializeField] private Image nextLineProgressFill;
    [SerializeField] private GameObject productivityCard;
    private float nextSummaryRefresh;

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
        lastTaskCount = -1; // A different factory can have the same number of orders.
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

        if (Time.unscaledTime >= nextSummaryRefresh)
        {
            nextSummaryRefresh = Time.unscaledTime + .5f;
            RefreshProductivityCard();
        }
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

    public void PrepareEditorView()
    {
        EnsureProductivityCard();
        if (!Application.isPlaying) productivityText.text = "员工 0/4 · 产能 0.00\n并行 1/3 · 速度 1.00x\n等待居民入驻";
    }

    private void EnsureProductivityCard()
    {
        if (productivityCard != null) return;

        productivityCard = new GameObject("产能概览", typeof(RectTransform), typeof(Image));
        productivityCard.transform.SetParent(transform, false);
        RectTransform cardRect = productivityCard.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(.01f, .25f);
        cardRect.anchorMax = new Vector2(.18f, .76f);
        cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;

        Image cardImage = productivityCard.GetComponent<Image>();
        ChimeraUITheme.StyleSurface(cardImage, ChimeraUITheme.Surface);
        cardImage.raycastTarget = true;

        GameObject textObject = new GameObject("产能文字", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(productivityCard.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 13f);
        textRect.offsetMax = new Vector2(-8f, -5f);
        productivityText = textObject.GetComponent<TextMeshProUGUI>();
        productivityText.alignment = TextAlignmentOptions.Center;
        productivityText.fontSize = 14f;
        productivityText.enableAutoSizing = true;
        productivityText.fontSizeMin = 12; productivityText.fontSizeMax = 14;
        productivityText.fontStyle = FontStyles.Bold;
        productivityText.color = ChimeraUITheme.PrimaryText;
        productivityText.lineSpacing = 0;
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
        backgroundImage.color = ChimeraUITheme.SurfaceDark;
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

        UIThemeBinding.Bind(productivityText, UIThemeRole.PrimaryText);
        UIThemeBinding.Bind(backgroundImage, UIThemeRole.Inset);
        UIThemeBinding.Bind(nextLineProgressFill, UIThemeRole.Accent);
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
        var logistics = LogisticsManager.Instance;
        if (logistics != null && logistics.Ready)
        {
            float input = logistics.Data.Storages.Where(s => s.OwnerID == boundFactory.PersistentID && s.Kind == StorageKind.OrderInput).Sum(s => s.Used);
            var output = logistics.Get(LogisticsManager.OutputID(boundFactory));
            productivityText.text += $"\n原料 {input:0.#} · 出货 {output?.Used ?? 0:0}/{boundFactory.OutputCapacity}";
        }

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
            CreateSlot(data.ChassisSprite, data.ChassisName, data);
        }
    }

    public void ShowComponentShelf()
    {
        ClearShelf();
        foreach (var data in PlayerInventoryManager.Instance.AllComponentDatabase)
        {
            CreateSlot(data.ComponentIcon, data.ComponentName, data);
        }
    }

    private void CreateSlot(Sprite icon, string itemName, Object sourceSO)
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

        var recipeName = slotObj.transform.Find("RecipeName")?.GetComponent<TMP_Text>();
        if(recipeName != null) { recipeName.text = itemName; recipeName.richText = false; }

        slotObj.GetComponent<Button>().onClick.AddListener(() => {
            ItemHoverTooltip.Hide();
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

        var font = GetComponentInParent<Canvas>()?.GetComponentInChildren<TMPro.TMP_Text>(true)?.font;
        ItemHoverTarget.Bind(slotObj, () => ItemHoverContent.Recipe(sourceSO, boundFactory), font);
    }

    // ==========================================
    // 📋 任务列表渲染
    // ==========================================

    private void RefreshQueueUI(FactoryBuilding factory)
    {
        // 1. 彻底清理旧格子
        foreach (Transform child in TaskQueueContainer) { child.gameObject.SetActive(false); Destroy(child.gameObject); }

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
                    // 由工厂取消材料预留，并把已领取材料交给物流退库。
                    factory.CancelTask(task);

                    // 然后再刷新 UI 表现
                    RefreshQueueUI(factory);
                }, factory);
            }
        }
    }

    private void ClearShelf()
    {
        foreach (Transform child in ShelfGrid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }
}
