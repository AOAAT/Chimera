using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>仓库窗口在运行时生成，场景只保留打开入口。</summary>
public class GlobalWarehouseUI : MonoBehaviour
{
    public static GlobalWarehouseUI Instance;
    public TMP_Dropdown MainCategoryDropdown;
    public TMP_Dropdown TypeDropdown;
    public TMP_Dropdown TagDropdown;
    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;
    public GameObject EmptyWarningText;

    private readonly List<ComponentType> types = new List<ComponentType>();
    private readonly List<SubTag> tags = new List<SubTag>();
    private TMP_FontAsset font;
    private TMP_Text summary, detailTitle, detailMeta, detailBody, pageText;
    private Image detailIcon;
    private ScrollRect itemScroll;
    private readonly List<Action> visibleItems = new List<Action>();
    private PlayerInventoryManager inventory;
    private bool built, dirty;
    private int page;
    private const int PageSize = 40;
    private readonly Color surface = new Color32(31, 45, 61, 255);
    private readonly Color ink = new Color32(240, 246, 252, 255);

    private void Awake()
    {
        Instance = this;
        UIBackHandler.Attach(gameObject, CloseWarehouse);
        gameObject.SetActive(false);
    }

    public void OpenWarehouse()
    {
        EnsureView();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BindInventory();
        dirty = true;
        RefreshWarehouse();
        MusicManager.Instance?.SetImmersionMode(true);
        ItemDetailPanelUI.Instance?.HidePanel();
    }

    public void CloseWarehouse()
    {
        MainCategoryDropdown?.Hide();
        TypeDropdown?.Hide();
        TagDropdown?.Hide();
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false);
        ItemDetailPanelUI.Instance?.HidePanel();
    }

    private void BindInventory()
    {
        if (inventory == PlayerInventoryManager.Instance) return;
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
        inventory = PlayerInventoryManager.Instance;
        if (inventory != null) inventory.OnInventoryChanged += MarkDirty;
    }

    private void MarkDirty() { dirty = true; }
    private void LateUpdate()
    {
        BindInventory();
        if (dirty && built) RefreshWarehouse();
    }
    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
        if (Instance == this) Instance = null;
    }

    private void EnsureView()
    {
        if (built) return;
        font = MainCategoryDropdown != null && MainCategoryDropdown.captionText != null
            ? MainCategoryDropdown.captionText.font : TMP_Settings.defaultFontAsset;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        // 独立 Overlay Canvas，避免父窗口缩放、遮罩、层级影响弹出菜单。
        transform.SetParent(null, false);
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = GetComponent<Image>();
        if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
        backdrop.sprite = null;
        backdrop.color = new Color32(6, 12, 20, 230);
        backdrop.raycastTarget = true;
        RectTransform window = Panel("WarehouseWindow", transform, new Color32(18, 28, 40, 255));
        Place(window, new Vector2(.09f, .1f), new Vector2(.91f, .9f));
        Text("仓 库", window, 30, new Vector2(.025f, .91f), new Vector2(.3f, .98f));
        summary = Text("", window, 18, new Vector2(.025f, .855f), new Vector2(.8f, .91f));
        Button("关闭  ×", window, new Vector2(.86f, .91f), new Vector2(.975f, .98f), CloseWarehouse);

        MainCategoryDropdown = Dropdown("资产类别", window, .025f, .25f);
        MainCategoryDropdown.ClearOptions();
        MainCategoryDropdown.AddOptions(new List<string> { "全部资产", "装甲底盘", "机甲组件", "逻辑配件" });
        TypeDropdown = Dropdown("组件类型", window, .265f, .49f);
        types.AddRange(Enum.GetValues(typeof(ComponentType)).Cast<ComponentType>());
        TypeDropdown.ClearOptions();
        TypeDropdown.AddOptions(new[] { "全部组件类型" }.Concat(types.Select(TypeName)).ToList());
        TagDropdown = Dropdown("流派标签", window, .505f, .73f);
        tags.AddRange(Enum.GetValues(typeof(SubTag)).Cast<SubTag>());
        TagDropdown.ClearOptions();
        TagDropdown.AddOptions(new[] { "全部流派" }.Concat(tags.Select(TagName)).ToList());
        MainCategoryDropdown.onValueChanged.AddListener(_ => FilterChanged());
        TypeDropdown.onValueChanged.AddListener(_ => FilterChanged());
        TagDropdown.onValueChanged.AddListener(_ => FilterChanged());

        itemScroll = Scroll(window, "ItemScroll", new Vector2(.025f, .115f), new Vector2(.64f, .77f));
        ContentRoot = itemScroll.content;
        var grid = ContentRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200, 166);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        var fitter = ContentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var empty = Text("当前筛选下没有物品", itemScroll.viewport, 22, new Vector2(.05f, .4f), new Vector2(.95f, .6f));
        empty.alignment = TextAlignmentOptions.Center;
        EmptyWarningText = empty.gameObject;

        var detail = Panel("ItemDetail", window, surface);
        Place(detail, new Vector2(.655f, .115f), new Vector2(.975f, .77f));
        detailIcon = Panel("Icon", detail, Color.white).GetComponent<Image>();
        Place(detailIcon.rectTransform, new Vector2(.04f, .79f), new Vector2(.24f, .97f));
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        detailTitle = Text("选择一件物品", detail, 25, new Vector2(.28f, .85f), new Vector2(.96f, .98f));
        detailMeta = Text("查看属性与制造品质", detail, 16, new Vector2(.04f, .70f), new Vector2(.96f, .8f));
        var detailScroll = Scroll(detail, "DetailScroll", new Vector2(.04f, .04f), new Vector2(.96f, .68f));
        detailBody = Text("", detailScroll.content, 18, Vector2.zero, Vector2.one);
        detailBody.overflowMode = TextOverflowModes.Overflow;
        var bodyLayout = detailScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandHeight = false;
        var bodyFit = detailScroll.content.gameObject.AddComponent<ContentSizeFitter>();
        bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Button("上一页", window, new Vector2(.025f, .035f), new Vector2(.12f, .09f), () => ChangePage(-1));
        pageText = Text("", window, 18, new Vector2(.135f, .035f), new Vector2(.34f, .09f));
        Button("下一页", window, new Vector2(.36f, .035f), new Vector2(.455f, .09f), () => ChangePage(1));
        Text("滚轮浏览  ·  点击查看详情", window, 17, new Vector2(.66f, .035f), new Vector2(.975f, .09f));
        built = true;
        FilterChanged();
    }

    private void FilterChanged()
    {
        page = 0;
        TypeDropdown.interactable = MainCategoryDropdown.value == 0 || MainCategoryDropdown.value == 2;
        dirty = true;
    }
    private void ChangePage(int delta)
    {
        page = Mathf.Clamp(page + delta, 0, Mathf.Max(0, (visibleItems.Count - 1) / PageSize));
        dirty = true;
    }
    private void RefreshWarehouse()
    {
        if (!built || !isActiveAndEnabled) return;
        dirty = false;
        foreach (Transform child in ContentRoot)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        visibleItems.Clear();
        ClearDetail();
        if (inventory == null) return;
        int category = MainCategoryDropdown.value;
        bool MatchesTags(List<SubTag> values) => TagDropdown.value == 0 ||
            (values != null && values.Contains(tags[TagDropdown.value - 1]));
        if (category == 0 || category == 1)
            foreach (ChassisStack stack in inventory.GetChassisStacks())
            {
                if (!MatchesTags(stack.BaseData.SubTags)) continue;
                for (int i = 0; i < stack.Quantity; i++)
                {
                    int number = i + 1;
                    var data = stack.BaseData;
                    visibleItems.Add(() => Card(data.ChassisName, $"装甲底盘 · 第 {number} 件", data.ChassisSprite,
                        ChimeraUITheme.SecondaryText, () => ShowDetail(data.ChassisName, "装甲底盘 · 标准规格", data.ChassisSprite,
                        data.Description, data.BaseStats, data.SpecialMechanicDesc)));
                }
            }
        if (category == 0 || category == 2)
            foreach (InstancedComponent component in inventory.GetAvailableComponents())
            {
                if (TypeDropdown.value > 0 && component.BaseData.Type != types[TypeDropdown.value - 1]) continue;
                if (!MatchesTags(component.BaseData.BaseSubTags)) continue;
                var item = component;
                visibleItems.Add(() => Card(item.BaseData.ComponentName,
                    $"Mk.{item.CurrentMark} · {ComponentQualityUtility.GetSummary(item.Quality, item.QualityScore)}",
                    item.BaseData.ComponentIcon, ComponentQualityUtility.GetColor(item.Quality), () => ShowComponent(item)));
            }
        if (category == 0 || category == 3)
            foreach (InstancedAccessory accessory in inventory.AccessoryInventory)
            {
                if (accessory?.BaseData == null || accessory.IsEquipped) continue;
                var item = accessory;
                if (TagDropdown.value > 0) continue;
                visibleItems.Add(() => Card(item.BaseData.AccessoryName, "逻辑配件", item.BaseData.AccessoryIcon,
                    ChimeraUITheme.Accent, () => ShowDetail(item.BaseData.AccessoryName, "逻辑配件", item.BaseData.AccessoryIcon,
                    item.BaseData.Description, item.BaseData.StaticStatModifiers, item.BaseData.SpecialMechanicDesc)));
            }
        int pages = Mathf.Max(1, Mathf.CeilToInt(visibleItems.Count / (float)PageSize));
        page = Mathf.Clamp(page, 0, pages - 1);
        for (int i = page * PageSize; i < Mathf.Min(visibleItems.Count, (page + 1) * PageSize); i++) visibleItems[i]();
        summary.text = $"可用物品 {visibleItems.Count} 件   /   组件独立品质 · 底盘标准规格";
        pageText.text = $"{page + 1} / {pages} 页";
        EmptyWarningText.SetActive(visibleItems.Count == 0);
        itemScroll.verticalNormalizedPosition = 1f;
        var layout = ContentRoot.GetComponent<GridLayoutGroup>();
        Canvas.ForceUpdateCanvases();
        float width = itemScroll.viewport.rect.width - 24;
        layout.cellSize = new Vector2(Mathf.Max(80, (width - 36) / 4), 166);
    }

    private void Card(string title, string meta, Sprite sprite, Color quality, Action select)
    {
        var card = Panel("AssetCard", ContentRoot, surface);
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.onClick.AddListener(() => { ItemDetailPanelUI.Instance?.HidePanel(); select(); });
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        var stripe = Panel("QualityStripe", card, quality);
        Place(stripe, new Vector2(0, .97f), Vector2.one);
        var icon = Panel("Icon", card, Color.white).GetComponent<Image>();
        Place(icon.rectTransform, new Vector2(.25f, .43f), new Vector2(.75f, .92f));
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.color = sprite != null ? Color.white : Color.clear;
        Text(title, card, 21, new Vector2(.07f, .21f), new Vector2(.93f, .42f));
        var label = Text(meta, card, 16, new Vector2(.07f, .04f), new Vector2(.95f, .21f));
        label.color = quality;
    }

    private void ClearDetail()
    {
        detailTitle.text = "选择一件物品";
        detailMeta.text = "点击左侧卡片查看属性";
        detailBody.text = "";
        detailIcon.color = Color.clear;
    }
    private void ShowComponent(InstancedComponent item)
    {
        string affixes = item.Affixes == null || item.Affixes.Count == 0 ? "无特殊词条" :
            string.Join("\n\n", item.Affixes.Where(a => a != null).Select(a => a.DisplayName + "\n" + a.Description));
        string identity = item.InstanceID ?? "";
        string notes = (item.BaseData.GetModelData(item.CurrentMark)?.SpecialMechanicDesc ?? "") +
            "\n\n" + affixes + "\n\n编号 " + identity.Substring(0, Math.Min(8, identity.Length));
        ShowDetail(item.BaseData.ComponentName,
            $"{TypeName(item.BaseData.Type)} · Mk.{item.CurrentMark}\n{ComponentQualityUtility.GetSummary(item.Quality, item.QualityScore)}",
            item.BaseData.ComponentIcon, item.BaseData.Description, ComponentStatResolver.Resolve(item), notes);
        detailMeta.color = ComponentQualityUtility.GetColor(item.Quality);
    }
    private void ShowDetail(string title, string meta, Sprite sprite, string description, List<StatEntry> stats, string notes)
    {
        detailTitle.text = title;
        detailMeta.text = meta;
        detailMeta.color = ink;
        detailIcon.sprite = sprite;
        detailIcon.color = sprite != null ? Color.white : Color.clear;
        var text = new StringBuilder(description).Append("\n\n属性\n");
        if (stats != null)
            foreach (StatEntry stat in stats.Where(s => s != null))
                text.Append(StatTranslation.Get(stat.StatID)).Append("    ")
                    .Append(stat.StatID == StatType.CriticalChance ? stat.Value.ToString("P1") : stat.Value.ToString("0.##"))
                    .Append(stat.ModType == BuffModifierType.Multiplier ? " ×" : "").Append("\n");
        detailBody.text = text.Append("\n").Append(notes).ToString();
        detailBody.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1f;
    }

    private RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }
    private TMP_Text Text(string value, Transform parent, float size, Vector2 min, Vector2 max)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = ink;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Place(label.rectTransform, min, max);
        return label;
    }
    private void Button(string label, Transform parent, Vector2 min, Vector2 max, Action click)
    {
        var rect = Panel("ActionButton", parent, new Color32(47, 72, 90, 255));
        Place(rect, min, max);
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => click());
        var text = Text(label, rect, 20, Vector2.zero, Vector2.one);
        text.alignment = TextAlignmentOptions.Center;
    }
    private TMP_Dropdown Dropdown(string name, Transform parent, float left, float right)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var dropdown = go.GetComponent<TMP_Dropdown>();
        Place((RectTransform)go.transform, new Vector2(left, .785f), new Vector2(right, .845f));
        go.GetComponent<Image>().color = surface;
        dropdown.template.GetComponent<Image>().color = surface;
        dropdown.template.sizeDelta = new Vector2(0, 280);
        Transform arrow = go.transform.Find("Arrow");
        if (arrow != null)
        {
            arrow.GetComponent<Image>().enabled = false;
            var arrowText = Text("v", arrow, 18, Vector2.zero, Vector2.one);
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.overflowMode = TextOverflowModes.Overflow;
        }
        foreach (Scrollbar scrollbar in dropdown.template.GetComponentsInChildren<Scrollbar>(true))
        {
            scrollbar.GetComponent<Image>().color = new Color32(18, 28, 40, 255);
            if (scrollbar.targetGraphic != null) scrollbar.targetGraphic.color = ChimeraUITheme.MutedText;
        }
        foreach (TMP_Text label in go.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = font;
            label.fontSize = 20;
            label.color = ink;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
        }
        var toggle = dropdown.template.GetComponentInChildren<Toggle>(true);
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.targetGraphic.color = new Color32(42, 62, 81, 255);
        toggle.graphic.color = ChimeraUITheme.Accent;
        var item = (RectTransform)toggle.transform;
        item.sizeDelta = new Vector2(item.sizeDelta.x, 36);
        var content = (RectTransform)item.parent;
        content.sizeDelta = new Vector2(content.sizeDelta.x, 40);
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = LayerMask.NameToLayer("UI");
        return dropdown;
    }
    private ScrollRect Scroll(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var root = Panel(name, parent, new Color32(13, 22, 33, 255));
        Place(root, min, max);
        var viewport = Panel("Viewport", root, Color.white);
        Place(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.gameObject.layer = LayerMask.NameToLayer("UI");
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        return scroll;
    }
    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    private static string TypeName(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心组件";
            case ComponentType.Weapon: return "武器组件";
            case ComponentType.Movement: return "移动组件";
            case ComponentType.Factory: return "生产组件";
            default: return "辅助组件";
        }
    }
    private static string TagName(SubTag tag)
    {
        string[] names = { "强酸", "近战", "远程", "冲撞", "装甲", "重型", "奉献", "强击", "击退",
            "废土", "工业", "枪械", "实验室", "装填", "动能", "等离子", "头颅", "内脏", "四肢", "寄生",
            "痛苦", "遗物", "异界", "魔力", "混沌", "秩序" };
        int index = (int)tag;
        return index >= 0 && index < names.Length ? names[index] : tag.ToString();
    }
}
