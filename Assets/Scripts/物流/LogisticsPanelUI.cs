using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Generated logistics dashboard; stable rows update their text without rebuilding buttons each tick.</summary>
public sealed class LogisticsPanelUI : MonoBehaviour
{
    public static LogisticsPanelUI Instance;
    [SerializeField] private RectTransform window, rows;
    [SerializeField] private TMP_Text summary, pageText;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Button openButton, closeButton, buildButton, previousButton, nextButton;
    [SerializeField] private Button[] tabButtons;
    private int tab, page;
    private string storageFilter, signature;
    private float nextRefresh;
    private readonly List<Action> updates = new List<Action>();
    private const int PageSize = 7;

    private void Start()
    {
        Instance = this;
        PrepareEditorView();
        BindControls();
        UIBackHandler.Attach(window.gameObject, () => window.gameObject.SetActive(false));
        window.gameObject.SetActive(false);
    }

    private void BindControls()
    {
        Bind(openButton, () => Open());
        Bind(closeButton, () => window.gameObject.SetActive(false));
        string[] tabs = { "搬运居民", "仓库与暂存区", "货物位置", "运输任务" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            Bind(tabButtons[i], () => { tab = index; page = 0; storageFilter = null; signature = null; Refresh(); });
        }
        Bind(buildButton, () =>
        {
            window.gameObject.SetActive(false);
            GlobalWarehouseUI.Instance?.CloseWarehouse();
            BuildingManager.Instance?.StartWarehousePlacement();
        });
        Bind(previousButton, () => { page = Mathf.Max(0, page - 1); signature = null; Refresh(); });
        Bind(nextButton, () => { page++; signature = null; Refresh(); });
    }
    private static void Bind(Button button, Action action)
    {
        if (button != null) button.onClick.AddListener(() => action());
    }

    public void PrepareEditorView()
    {
        if (window != null) { CacheButtons(); return; }
        gameObject.layer = LayerMask.NameToLayer("UI");
        var sample = Resources.FindObjectsOfTypeAll<TMP_Text>().FirstOrDefault(x => x.font != null && x.font.name.Contains("msyh"));
        if (sample == null) sample = Resources.FindObjectsOfTypeAll<TMP_Text>().FirstOrDefault(x => x.font != null && x.gameObject.scene.IsValid());
        font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 210;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        MakeButton("物流管理", transform, new Vector2(.46f, .95f), new Vector2(.54f, .99f), null);
        window = Panel("LogisticsWindow", transform, ChimeraUITheme.Window, new Vector2(.12f, .12f), new Vector2(.88f, .88f));
        Label("殖民地物流", window, 28, new Vector2(.025f, .9f), new Vector2(.4f, .98f));
        MakeButton("关闭 ×", window, new Vector2(.87f, .91f), new Vector2(.975f, .97f), null);
        string[] tabs = { "搬运居民", "仓库与暂存区", "货物位置", "运输任务" };
        for (int i = 0; i < tabs.Length; i++)
            MakeButton(tabs[i], window, new Vector2(.025f + i * .19f, .81f), new Vector2(.20f + i * .19f, .88f), null);
        MakeButton("新建仓库", window, new Vector2(.80f, .81f), new Vector2(.975f, .88f), null);
        summary = Label("仓库与居民数据在运行时更新", window, 18, new Vector2(.025f, .73f), new Vector2(.975f, .80f));
        rows = Panel("Rows", window, Color.clear, new Vector2(.025f, .14f), new Vector2(.975f, .73f));
        rows.GetComponent<Image>().raycastTarget = false;
        MakeButton("上一页", window, new Vector2(.025f, .04f), new Vector2(.13f, .11f), null);
        pageText = Label("1 / 1 页", window, 17, new Vector2(.15f, .04f), new Vector2(.28f, .11f));
        MakeButton("下一页", window, new Vector2(.29f, .04f), new Vector2(.40f, .11f), null);
        Label("在岗居民需先下岗 · 右键指令优先 · 成品入库后可用于组装", window, 17, new Vector2(.43f, .04f), new Vector2(.975f, .11f));
        CacheButtons();
        window.gameObject.SetActive(false);
    }
    private void CacheButtons()
    {
        if (openButton != null && tabButtons != null && tabButtons.Length == 4) return;
        openButton = transform.Find("物流管理").GetComponent<Button>();
        closeButton = window.Find("关闭 ×").GetComponent<Button>();
        buildButton = window.Find("新建仓库").GetComponent<Button>();
        previousButton = window.Find("上一页").GetComponent<Button>();
        nextButton = window.Find("下一页").GetComponent<Button>();
        tabButtons = new[] { "搬运居民", "仓库与暂存区", "货物位置", "运输任务" }
            .Select(name => window.Find(name).GetComponent<Button>()).ToArray();
    }
    public void Open(string storageID = null)
    {
        if (window == null) return;
        if (storageID != null) { tab = 2; storageFilter = storageID; }
        window.gameObject.SetActive(true); signature = null; page = 0; Refresh();
    }
    private void Update()
    {
        if (window == null || !window.gameObject.activeSelf || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .5f; Refresh();
    }
    private void Refresh()
    {
        var manager = LogisticsManager.Instance;
        if (manager == null || !manager.Ready) { summary.text = "等待地图与初始仓库就绪"; return; }
        var residents = PopulationManager.Instance != null ? PopulationManager.Instance.TotalResidents.Where(x => x != null).ToList() : new List<ResidentData>();
        var stores = manager.Data.Storages.Where(x => x.Kind != StorageKind.Backpack || x.Used > 0).ToList();
        var cargo = stores.Where(x => storageFilter == null || x.ID == storageFilter)
            .SelectMany(s => s.Cargo.Select(c => (store: s, cargo: c))).ToList();
        var jobs = manager.Data.Jobs.ToList();
        int count = tab == 0 ? residents.Count : tab == 1 ? stores.Count : tab == 2 ? cargo.Count : jobs.Count;
        int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize)); page = Mathf.Clamp(page, 0, pages - 1);
        string nextSignature = tab + ":" + page + ":" + string.Join("|", tab == 0 ? residents.Select(x => x.InstanceID) : tab == 1 ? stores.Select(x => x.ID) :
            tab == 2 ? cargo.Select(x => x.store.ID + x.cargo.Key) : jobs.Select(x => x.ID));
        int availableWorkers = ResidentEntity.ActiveResidents.Count(x => x != null && x.MyData != null && x.MyData.HaulingEnabled && x.MyData.Status == ResidentStatus.Idle);
        summary.text = $"仓库 {manager.Warehouses.Count()} · 搬运居民 {availableWorkers} · 运输任务 {jobs.Count}" +
            (availableWorkers == 0 && jobs.Count > 0 ? "  /  请在“搬运居民”中启用搬运" : "") +
            (storageFilter != null ? "  /  " + manager.Get(storageFilter)?.Name : "");
        pageText.text = $"{page + 1} / {pages} 页";
        if (signature != nextSignature)
        {
            signature = nextSignature; updates.Clear();
            foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int index = page * PageSize; index < Mathf.Min(count, (page + 1) * PageSize); index++)
            {
                int rowIndex = index - page * PageSize;
                var row = Panel("Row", rows, ChimeraUITheme.SurfaceDark, new Vector2(0, 1 - (rowIndex + 1) / 7f + .008f), new Vector2(1, 1 - rowIndex / 7f - .008f));
                var label = Label("", row, 18, new Vector2(.015f, .06f), new Vector2(tab <= 1 ? .72f : .985f, .94f));
                if (tab == 0)
                {
                    var resident = residents[index];
                    var toggle = MakeButton("", row, new Vector2(.79f, .18f), new Vector2(.985f, .82f), () =>
                    {
                        resident.HaulingEnabled = !resident.HaulingEnabled;
                        var entity = ResidentEntity.ActiveResidents.FirstOrDefault(x => x != null && x.MyData == resident);
                        if (entity != null && !resident.HaulingEnabled) manager.Interrupt(entity);
                        Refresh();
                    });
                    updates.Add(() =>
                    {
                        var entity = ResidentEntity.ActiveResidents.FirstOrDefault(x => x != null && x.MyData == resident);
                        label.text = resident.ResidentName + "\n" + manager.WorkerSummary(resident) + (entity != null && !string.IsNullOrEmpty(entity.LogisticsIssue) ? " · " + entity.LogisticsIssue : "");
                        toggle.text = resident.HaulingEnabled ? "搬运：开启" : "搬运：关闭";
                    });
                }
                else if (tab == 1)
                {
                    var store = stores[index];
                    MakeButton("库存", row, new Vector2(.73f, .18f), new Vector2(.81f, .82f), () => Open(store.ID));
                    if (store.Kind == StorageKind.Warehouse)
                    {
                        var resources = MakeButton("", row, new Vector2(.82f, .18f), new Vector2(.895f, .82f), () => { store.AcceptResources = !store.AcceptResources; Refresh(); });
                        var items = MakeButton("", row, new Vector2(.905f, .18f), new Vector2(.985f, .82f), () => { store.AcceptItems = !store.AcceptItems; Refresh(); });
                        updates.Add(() => { resources.text = store.AcceptResources ? "资源 ✓" : "资源 ×"; items.text = store.AcceptItems ? "组件 ✓" : "组件 ×"; });
                    }
                    updates.Add(() => label.text = store.Name + $"\n容量 {store.Used:0.#}/{(store.Kind == StorageKind.Recovery ? "暂存" : store.Capacity.ToString("0.#"))} · 入库预留 {manager.ReservedIn(store.ID):0.#}");
                }
                else if (tab == 2)
                {
                    var item = cargo[index];
                    updates.Add(() => label.text = LogisticsKeys.Name(item.cargo.Key) + $" × {item.store.Count(item.cargo.Key):0.#}\n{item.store.Name} · 已预留 {manager.ReservedOut(item.store.ID, item.cargo.Key):0.#}" +
                        (item.cargo.Key.StartsWith("component:") ? " · " + item.cargo.Key.Substring(10, Mathf.Min(8, item.cargo.Key.Length - 10)) : ""));
                }
                else
                {
                    var job = jobs[index];
                    updates.Add(() => label.text = $"{LogisticsKeys.Name(job.Key)} × {job.Amount:0.#} · " +
                        (job.RetryAt > Time.time ? "通道受阻，等待重试" : string.IsNullOrEmpty(job.WorkerID) ? "等待搬运工" : job.PickedUp ? "运送中" : "前往取货") +
                        "\n" + manager.Get(job.SourceID)?.Name + " → " + manager.Get(job.TargetID)?.Name);
                }
            }
        }
        foreach (var update in updates) update();
    }
    private RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); Place(rect, min, max);
        var image = go.GetComponent<Image>(); image.color = color;
        image.sprite = Resources.Load<Sprite>("UI/ChimeraRounded"); image.type = Image.Type.Sliced;
        return rect;
    }
    private TMP_Text Label(string text, Transform parent, float size, Vector2 min, Vector2 max)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size;
        label.color = ChimeraUITheme.PrimaryText; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.overflowMode = TextOverflowModes.Ellipsis;
        Place(label.rectTransform, min, max); return label;
    }
    private TMP_Text MakeButton(string text, Transform parent, Vector2 min, Vector2 max, Action action)
    {
        var rect = Panel(text, parent, ChimeraUITheme.Button, min, max);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        if (action != null) button.onClick.AddListener(() => action());
        var label = Label(text, rect, 18, new Vector2(.03f, .05f), new Vector2(.97f, .95f)); label.alignment = TextAlignmentOptions.Center;
        return label;
    }
    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
