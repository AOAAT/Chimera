using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单座仓库库存；永久物品身份，四列滚动视口，编辑器保存布局。</summary>
public class GlobalWarehouseUI : MonoBehaviour
{
    public static GlobalWarehouseUI Instance;
    public TMP_Dropdown MainCategoryDropdown, TypeDropdown, TagDropdown;
    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;
    public GameObject EmptyWarningText;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TMP_Text summary, detailTitle, detailMeta, detailBody;
    [SerializeField] private Image detailIcon;
    [SerializeField] private ScrollRect itemScroll;
    [SerializeField] private TMP_Dropdown warehouseDropdown;
    [SerializeField] private TMP_InputField searchInput, renameInput;
    [SerializeField] private Button closeButton, renameButton, saveNameButton, cancelNameButton;
    [SerializeField] private GameObject renameRow;
    [SerializeField] private int viewVersion;
    private const int Version = 3;
    private readonly List<ComponentType> types = Enum.GetValues(typeof(ComponentType)).Cast<ComponentType>().ToList();
    private readonly List<SubTag> tags = Enum.GetValues(typeof(SubTag)).Cast<SubTag>().ToList();
    private readonly List<string> warehouseIDs = new List<string>();
    private readonly List<Entry> entries = new List<Entry>();
    private PlayerInventoryManager inventory;
    private string storageID, selectedKey, fingerprint;
    private bool opening, controlsBound, dirty, resetScroll;
    private int firstRow = -1;
    private Vector2 viewportSize;
    private float nextCheck;
    private class Entry
    {
        public string Key, Name, Original, Meta, Description, Notes;
        public Sprite Icon;
        public List<StatEntry> Stats;
        public string CardMeta;
        public Color Quality = ChimeraUITheme.MutedText;
        public bool Rename;
    }
    private sealed class CardView
    {
        public RectTransform Rect;
        public Image Icon, Stripe;
        public TMP_Text Title, Meta, ResourceGlyph;
        public Button Button;
        public Entry Entry;
    }
    private readonly List<CardView> cardPool = new List<CardView>();
    public string StorageID => storageID;
    public int FilteredCount => entries.Count;
    public TMP_InputField SearchInput => searchInput;
    private void Awake()
    {
        Instance = this;
        UIBackHandler.Attach(gameObject, CloseWarehouse);
        if (!opening) gameObject.SetActive(false);
    }
    public void OpenWarehouse()
    {
        var selected = SelectionContextHUD.Instance?.CurrentTargetBuilding as WarehouseBuilding;
        OpenWarehouse(selected != null ? selected.PersistentID : storageID);
    }
    public void OpenWarehouse(string id)
    {
        EnsureView();
        storageID = id;
        opening = true; gameObject.SetActive(true); opening = false;
        Instance = this;
        transform.SetAsLastSibling();
        BindInventory(); RefreshWarehouseOptions();
        dirty = resetScroll = true;
        RefreshWarehouse();
        MusicManager.Instance?.SetImmersionMode(true);
        ItemHoverTooltip.Hide();
    }
    public void CloseWarehouse()
    {
        MainCategoryDropdown?.Hide(); TypeDropdown?.Hide(); TagDropdown?.Hide(); warehouseDropdown?.Hide();
        renameRow?.SetActive(false);
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false);
        ItemHoverTooltip.Hide();
    }
    private void BindInventory()
    {
        if (inventory == PlayerInventoryManager.Instance) return;
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
        inventory = PlayerInventoryManager.Instance;
        if (inventory != null) inventory.OnInventoryChanged += MarkDirty;
    }
    private void MarkDirty() => dirty = true;
    private void LateUpdate()
    {
        BindInventory();
        if (viewVersion != Version) return;
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + .25f;
            RefreshWarehouseOptions();
            var store = LogisticsManager.Instance?.Get(storageID);
            string value = store == null ? "missing" : store.Name + "|" + store.Capacity + "|" +
                string.Join(";", store.Cargo.Select(c => c.Key + ":" + c.Amount));
            if (value != fingerprint) { fingerprint = value; dirty = true; }
        }
        if (dirty) RefreshWarehouse();
        if (itemScroll.viewport.rect.size != viewportSize) RenderVisible(true);
    }
    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
        if (Instance == this) Instance = null;
    }
    public void PrepareEditorView() => EnsureView();
    private void BindControls()
    {
        if (!Application.isPlaying || controlsBound) return;
        controlsBound = true;
        closeButton.onClick.AddListener(CloseWarehouse);
        MainCategoryDropdown.onValueChanged.AddListener(_ => FilterChanged());
        TypeDropdown.onValueChanged.AddListener(_ => FilterChanged());
        TagDropdown.onValueChanged.AddListener(_ => FilterChanged());
        searchInput.onValueChanged.AddListener(_ => FilterChanged());
        warehouseDropdown.onValueChanged.AddListener(i =>
        {
            if (i < 0 || i >= warehouseIDs.Count) return;
            storageID = warehouseIDs[i]; selectedKey = null; renameRow.SetActive(false);
            dirty = resetScroll = true;
        });
        renameButton.onClick.AddListener(BeginRename);
        saveNameButton.onClick.AddListener(CommitRename);
        renameInput.onSubmit.AddListener(_ => CommitRename());
        cancelNameButton.onClick.AddListener(() => renameRow.SetActive(false));
        itemScroll.onValueChanged.AddListener(_ => RenderVisible(false));
    }
    private void EnsureView()
    {
        if (viewVersion == Version) { BindControls(); return; }
        font = font != null ? font : MainCategoryDropdown?.captionText?.font ?? TMP_Settings.defaultFontAsset;
        foreach (Transform child in transform.Cast<Transform>().ToArray())
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        controlsBound = false; cardPool.Clear();
        transform.SetParent(null, false);
        var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        backdrop.sprite = null; backdrop.overrideSprite = null;
        backdrop.color = ChimeraUITheme.Backdrop; backdrop.raycastTarget = true;
        var window = Panel("WarehouseWindow", transform, ChimeraUITheme.Window);
        Place(window, new Vector2(.07f, .08f), new Vector2(.93f, .92f));
        warehouseDropdown = Dropdown("WarehouseSelector", window, new Vector2(.025f,.90f), new Vector2(.28f,.974f), new List<string>{ "仓库" });
        summary = Text("已用容量 — / —", window, 22, new Vector2(.30f,.90f), new Vector2(.86f,.974f));
        closeButton = Button("×", window, new Vector2(.94f,.91f), new Vector2(.982f,.974f));
        MainCategoryDropdown = Dropdown("Category", window, new Vector2(.025f,.805f),new Vector2(.16f,.874f),new List<string>{"全部类别","装甲底盘","机甲组件","基础资源"});
        TypeDropdown = Dropdown("ComponentType", window,new Vector2(.17f,.805f),new Vector2(.31f,.874f),new[]{"组件类型"}.Concat(types.Select(TypeName)).ToList());
        TagDropdown = Dropdown("Tag",window,new Vector2(.32f,.805f),new Vector2(.46f,.874f),new[]{"全部标签"}.Concat(tags.Select(TagName)).ToList());
        searchInput = Input("Search",window,"输入名称查找",new Vector2(.47f,.805f),new Vector2(.64f,.874f));
        itemScroll = Scroll(window,"ItemScroll",new Vector2(.025f,.04f),new Vector2(.64f,.78f));
        ContentRoot = itemScroll.content;
        var empty = Text("当前仓库没有符合条件的物品",itemScroll.viewport,22,new Vector2(.05f,.4f),new Vector2(.95f,.6f));
        empty.alignment = TextAlignmentOptions.Center; EmptyWarningText=empty.gameObject;
        var detail=Panel("ItemDetail",window,ChimeraUITheme.Surface);
        Place(detail,new Vector2(.66f,.04f),new Vector2(.975f,.874f));
        detailIcon=Panel("Icon",detail,Color.clear,false).GetComponent<Image>();
        Place(detailIcon.rectTransform,new Vector2(.05f,.79f),new Vector2(.28f,.97f));detailIcon.preserveAspect=true;
        detailTitle=Text("选择一件物品",detail,25,new Vector2(.33f,.85f),new Vector2(.83f,.97f));
        detailTitle.richText=false;detailTitle.enableAutoSizing=true;detailTitle.fontSizeMin=18;detailTitle.fontSizeMax=25;
        renameButton=Button("改名",detail,new Vector2(.84f,.875f),new Vector2(.975f,.95f));
        detailMeta=Text("",detail,18,new Vector2(.05f,.67f),new Vector2(.95f,.79f));
        var detailScroll=Scroll(detail,"DetailScroll",new Vector2(.045f,.035f),new Vector2(.955f,.65f));
        detailBody=Text("",detailScroll.content,20,Vector2.zero,Vector2.one);
        detailBody.overflowMode=TextOverflowModes.Overflow;
        var layout=detailScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
        layout.padding=new RectOffset(10,22,8,8);
        detailScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        renameRow=Panel("RenameRow",detail,ChimeraUITheme.Header).gameObject;
        Place((RectTransform)renameRow.transform,new Vector2(.02f,.66f),new Vector2(.98f,.86f));
        renameInput=Input("CustomName",renameRow.transform,"留空恢复原名",new Vector2(.03f,.49f),new Vector2(.97f,.96f));
        renameInput.characterLimit=24;
        saveNameButton=Button("保存",renameRow.transform,new Vector2(.49f,.05f),new Vector2(.73f,.43f));
        cancelNameButton=Button("取消",renameRow.transform,new Vector2(.75f,.05f),new Vector2(.97f,.43f));
        Text("最多24字",renameRow.transform,16,new Vector2(.04f,.05f),new Vector2(.46f,.43f));
        renameRow.SetActive(false);
        viewVersion=Version; ClearDetail(); BindControls();
    }
    private void RefreshWarehouseOptions()
    {
        var stores=LogisticsManager.Instance?.Warehouses.ToList() ?? new List<LogisticsStorage>();
        if (stores.Count==0) { storageID=null; return; }
        if (!stores.Any(x=>x.ID==storageID)) { storageID=stores[0].ID; dirty=resetScroll=true; }
        if (!warehouseIDs.SequenceEqual(stores.Select(x=>x.ID)) || !warehouseDropdown.options.Select(x=>x.text).SequenceEqual(stores.Select(x=>x.Name)))
        {
            warehouseIDs.Clear();warehouseIDs.AddRange(stores.Select(x=>x.ID));
            warehouseDropdown.ClearOptions();warehouseDropdown.AddOptions(stores.Select(x=>x.Name).ToList());
        }
        warehouseDropdown.SetValueWithoutNotify(warehouseIDs.IndexOf(storageID));
    }
    private void FilterChanged()
    {
        TypeDropdown.interactable=MainCategoryDropdown.value==0 || MainCategoryDropdown.value==2;
        dirty=resetScroll=true;
    }
    private bool Matches(string name,string original,List<SubTag> itemTags,int category,ComponentType? type=null)
    {
        int filter=MainCategoryDropdown.value;
        if(filter!=0 && filter!=category)return false;
        if(TypeDropdown.interactable && TypeDropdown.value>0 && type!=types[TypeDropdown.value-1])return false;
        if(TagDropdown.value>0 && (itemTags==null || !itemTags.Contains(tags[TagDropdown.value-1])))return false;
        string query=searchInput.text.Trim();
        return query.Length==0 || (name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || (original??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
    }
    private void RefreshWarehouse()
    {
        dirty=false; entries.Clear();
        var store=LogisticsManager.Instance?.Get(storageID);
        summary.text=store==null?"尚无可用仓库":$"已用容量 {store.Used:0.#} / {store.Capacity:0.#}";
        if(store!=null && inventory!=null)
        foreach(var cargo in store.Cargo.Where(x=>x.Amount>0).OrderBy(x=>x.Key))
        {
            Entry entry=null;
            if(cargo.Key.StartsWith("component:"))
            {
                var item=inventory.GetComponentInstance(cargo.Key.Substring(10));
                if(item?.BaseData==null || !Matches(item.DisplayName,item.BaseData.ComponentName,item.BaseData.BaseSubTags,2,item.BaseData.Type))continue;
                entry=new Entry{Key=cargo.Key,Name=item.DisplayName,Original=item.BaseData.ComponentName,Icon=item.BaseData.ComponentIcon,Rename=true,
                    Meta=$"{TypeName(item.BaseData.Type)} · Mk.{item.CurrentMark} · {ComponentQualityUtility.GetName(item.Quality)}",
                    CardMeta=$"Mk.{item.CurrentMark} · {ComponentQualityUtility.GetName(item.Quality)}",
                    Quality=ChimeraUITheme.QualityTextColor(item.Quality),
                    Description=item.BaseData.Description,Stats=ComponentStatResolver.Resolve(item),
                    Notes=string.Join("\n\n",(item.Affixes??new List<ComponentAffixInstance>()).Where(x=>x!=null).Select(x=>x.DisplayName+"\n"+x.Description))};
            }
            else if(cargo.Key.StartsWith("chassis-instance:"))
            {
                var item=inventory.GetChassisInstance(cargo.Key.Substring(17));
                if(item?.BaseData==null || !Matches(item.DisplayName,item.BaseData.ChassisName,item.BaseData.SubTags,1))continue;
                entry=new Entry{Key=cargo.Key,Name=item.DisplayName,Original=item.BaseData.ChassisName,Icon=item.BaseData.ChassisSprite,Rename=true,
                    Meta="装甲底盘 · 标准规格",Description=item.BaseData.Description,Stats=item.BaseData.BaseStats,Notes=item.BaseData.SpecialMechanicDesc};
            }
            else if(LogisticsKeys.IsResource(cargo.Key))
            {
                string name=LogisticsKeys.Name(cargo.Key);
                if(!Matches(name,name,null,3))continue;
                entry=new Entry{Key=cargo.Key,Name=name,Original=name,Meta=$"基础资源 · {cargo.Amount:0.#}",Description="存放于当前仓库的生产与建造材料。",Notes=$"库存 {cargo.Amount:0.#}\n可用 {LogisticsManager.Instance.Available(store,cargo.Key):0.#}"};
            }
            if(entry!=null)entries.Add(entry);
        }
        EmptyWarningText.SetActive(entries.Count==0);
        if(resetScroll){itemScroll.StopMovement();itemScroll.verticalNormalizedPosition=1;resetScroll=false;}
        RenderVisible(true);
        var selected=entries.Find(x=>x.Key==selectedKey);
        if(selected!=null)ShowDetail(selected,false);else{selectedKey=null;ClearDetail();}
    }
    // Virtual rows keep a very large warehouse inexpensive; 12 cards visible + overscan.
    private void RenderVisible(bool force)
    {
        if(itemScroll==null || ContentRoot==null)return;
        viewportSize=itemScroll.viewport.rect.size;
        float width=Mathf.Max(80,(viewportSize.x-40)/4),height=Mathf.Max(60,(viewportSize.y-32)/3);
        int row=Mathf.Max(0,Mathf.FloorToInt(itemScroll.content.anchoredPosition.y/(height+8)));
        if(!force && row==firstRow)return;firstRow=row;
        itemScroll.content.sizeDelta=new Vector2(0,Mathf.Max(viewportSize.y,Mathf.Ceil(entries.Count/4f)*(height+8)+8));
        int start=Mathf.Max(0,row-1)*4,end=Mathf.Min(entries.Count,(row+5)*4);
        int visibleCount=Mathf.Max(0,end-start);
        for(int n=0;n<visibleCount;n++)
        {
            if(n==cardPool.Count) cardPool.Add(CreateCard());
            int i=start+n; var entry=entries[i]; var view=cardPool[n]; var card=view.Rect;
            view.Entry=entry; card.gameObject.SetActive(true);
            card.anchoredPosition=new Vector2(8+(i%4)*(width+8),-8-(i/4)*(height+8));card.sizeDelta=new Vector2(width,height);
            UIThemeBinding.Bind(card.GetComponent<Image>(),entry.Key==selectedKey ? UIThemeRole.Header : UIThemeRole.Surface,true);
            var border=card.GetComponent<Outline>();
            if(entry.Key==selectedKey) { border.enabled=true; border.effectColor=ChimeraUITheme.Accent; border.effectDistance=new Vector2(2,-2); }
            view.Icon.sprite=entry.Icon;view.Icon.color=entry.Icon!=null ? Color.white : Color.clear;
            view.ResourceGlyph.gameObject.SetActive(entry.Icon==null);
            view.ResourceGlyph.text=string.IsNullOrEmpty(entry.Name)?"?":entry.Name.Substring(0,1);
            view.Title.text=entry.Name;view.Meta.text=entry.CardMeta??entry.Meta;view.Meta.color=entry.Quality;view.Stripe.color=entry.Quality;
        }
        for(int n=visibleCount;n<cardPool.Count;n++)cardPool[n].Rect.gameObject.SetActive(false);
    }
    private CardView CreateCard()
    {
        var view=new CardView();
        view.Rect=Panel("AssetCard",ContentRoot,ChimeraUITheme.Surface);
        view.Rect.anchorMin=view.Rect.anchorMax=new Vector2(0,1);view.Rect.pivot=new Vector2(0,1);
        view.Button=view.Rect.gameObject.AddComponent<Button>();view.Button.targetGraphic=view.Rect.GetComponent<Image>();
        view.Button.onClick.AddListener(()=>
        {
            if(view.Entry==null)return;
            selectedKey=view.Entry.Key;renameRow.SetActive(false);ShowDetail(view.Entry,true);RenderVisible(true);
        });
        view.Icon=Panel("Icon",view.Rect,Color.clear,false).GetComponent<Image>();
        Place(view.Icon.rectTransform,new Vector2(.22f,.36f),new Vector2(.78f,.91f));view.Icon.preserveAspect=true;
        view.ResourceGlyph=Text("",view.Rect,36,new Vector2(.32f,.45f),new Vector2(.68f,.83f));view.ResourceGlyph.alignment=TextAlignmentOptions.Center;
        view.Title=Text("",view.Rect,22,new Vector2(.06f,.16f),new Vector2(.94f,.35f));view.Title.richText=false;
        view.Title.enableAutoSizing=true;view.Title.fontSizeMin=16;view.Title.fontSizeMax=22;
        view.Title.alignment=TextAlignmentOptions.Center;view.Title.fontStyle=FontStyles.Bold;
        view.Meta=Text("",view.Rect,16,new Vector2(.04f,.025f),new Vector2(.96f,.16f));view.Meta.alignment=TextAlignmentOptions.Center;
        UIThemeBinding.Bind(view.Meta,UIThemeRole.None,false,false);
        view.Stripe=Panel("QualityStripe",view.Rect,Color.clear,false).GetComponent<Image>();
        Place(view.Stripe.rectTransform,new Vector2(.03f,.96f),new Vector2(.97f,.98f));
        return view;
    }
    private void ClearDetail()
    {
        detailTitle.text="选择一件物品";detailMeta.text="点击左侧卡片查看属性";detailBody.text="";detailIcon.color=Color.clear;
        renameButton.interactable=false;renameRow.SetActive(false);
    }
    private void ShowDetail(Entry entry,bool reset)
    {
        detailTitle.text=entry.Name;detailMeta.text="原型："+entry.Original+"\n"+entry.Meta;
        detailIcon.sprite=entry.Icon;detailIcon.color=entry.Icon!=null?Color.white:Color.clear;
        renameButton.interactable=entry.Rename;
        var body=new StringBuilder("<b>基础属性</b>\n");
        if(entry.Stats!=null)foreach(var stat in entry.Stats.Where(x=>x!=null))
            body.Append(StatTranslation.Get(stat.StatID)).Append("    ").Append(stat.StatID==StatType.CriticalChance && stat.ModType!=BuffModifierType.Multiplier ? stat.Value.ToString("P1") : stat.Value.ToString("0.##")).Append(stat.ModType==BuffModifierType.Multiplier?" ×":"").Append("\n");
        body.Append("\n<b>特殊说明</b>\n").Append(string.IsNullOrEmpty(entry.Notes)?"无":entry.Notes).Append("\n\n<b>物品描述</b>\n").Append(entry.Description);
        detailBody.text=body.ToString();
        if(reset)detailBody.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;
    }
    private void BeginRename()
    {
        var selected=entries.Find(x=>x.Key==selectedKey);if(selected==null || !selected.Rename)return;
        renameRow.SetActive(true);renameInput.SetTextWithoutNotify(selected.Name);renameInput.Select();renameInput.ActivateInputField();
    }
    private void CommitRename()
    {
        if(!renameRow.activeSelf || inventory==null || selectedKey==null)return;
        var store=LogisticsManager.Instance?.Get(storageID);
        if(store==null || store.Count(selectedKey)<1){renameRow.SetActive(false);dirty=true;return;}
        if(!inventory.RenameItem(selectedKey,renameInput.text)){UIFeedback.Show("名称最多24字，请勿使用换行或尖括号。");return;}
        renameRow.SetActive(false);dirty=true;
    }
    private RectTransform Panel(string name,Transform parent,Color color,bool framed=true)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=color;
        if(framed)ChimeraUITheme.StyleSurface(image,color);
        image.raycastTarget=framed;
        return (RectTransform)go.transform;
    }
    private TMP_Text Text(string value,Transform parent,float size,Vector2 min,Vector2 max)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=5;go.transform.SetParent(parent,false);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.text=value;label.fontSize=size;label.color=ChimeraUITheme.PrimaryText;
        UIThemeBinding.Bind(label,UIThemeRole.PrimaryText);
        label.raycastTarget=false;label.enableWordWrapping=true;label.overflowMode=TextOverflowModes.Ellipsis;Place(label.rectTransform,min,max);return label;
    }
    private Button Button(string value,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=Panel("Action_"+value,parent,ChimeraUITheme.Button);Place(rect,min,max);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
        var label=Text(value,rect,20,Vector2.zero,Vector2.one);label.alignment=TextAlignmentOptions.Center;
        bool accent=value=="保存" || value=="改名";
        UIThemeBinding.Bind(rect.GetComponent<Image>(),accent ? UIThemeRole.AccentButton : UIThemeRole.Button,true);
        UIThemeBinding.Bind(label,accent ? UIThemeRole.OnAccent : UIThemeRole.PrimaryText);
        label.fontStyle=FontStyles.Bold;return button;
    }
    private TMP_InputField Input(string name,Transform parent,string placeholder,Vector2 min,Vector2 max)
    {
        var go=TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());go.name=name;go.transform.SetParent(parent,false);Place((RectTransform)go.transform,min,max);
        var input=go.GetComponent<TMP_InputField>();ChimeraUITheme.StyleSurface(go.GetComponent<Image>(),ChimeraUITheme.Surface);
        foreach(var text in go.GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.fontSize=20;text.color=ChimeraUITheme.PrimaryText;text.richText=false;}
        ((TMP_Text)input.placeholder).text=placeholder;UIThemeBinding.Bind(input.placeholder,UIThemeRole.MutedText);
        UIThemeBinding.Bind(input.textComponent,UIThemeRole.PrimaryText);return input;
    }
    private TMP_Dropdown Dropdown(string name,Transform parent,Vector2 min,Vector2 max,List<string> options)
    {
        var go=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());go.name=name;go.transform.SetParent(parent,false);Place((RectTransform)go.transform,min,max);
        var dropdown=go.GetComponent<TMP_Dropdown>();dropdown.ClearOptions();dropdown.AddOptions(options);
        ChimeraUITheme.StyleSurface(go.GetComponent<Image>(),ChimeraUITheme.Surface);
        ChimeraUITheme.StyleSurface(dropdown.template.GetComponent<Image>(),ChimeraUITheme.Window);
        dropdown.template.sizeDelta=new Vector2(0,280);
        foreach(var text in go.GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.fontSize=20;text.color=ChimeraUITheme.PrimaryText;text.raycastTarget=false;UIThemeBinding.Bind(text,UIThemeRole.PrimaryText);}
        var arrow=go.transform.Find("Arrow");if(arrow!=null){arrow.GetComponent<Image>().enabled=false;Text("v",arrow,18,Vector2.zero,Vector2.one).alignment=TextAlignmentOptions.Center;}
        var toggle=dropdown.template.GetComponentInChildren<Toggle>(true);toggle.targetGraphic.color=ChimeraUITheme.Header;toggle.graphic.color=ChimeraUITheme.Accent;
        var item=(RectTransform)toggle.transform;item.sizeDelta=new Vector2(item.sizeDelta.x,36);
        ((RectTransform)item.parent).sizeDelta=new Vector2(0,40);
        return dropdown;
    }
    private ScrollRect Scroll(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var root=Panel(name,parent,ChimeraUITheme.Window);Place(root,min,max);
        var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();viewport.SetParent(root,false);Place(viewport,Vector2.zero,Vector2.one);
        var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        var bar=Panel("Scrollbar",root,ChimeraUITheme.SurfaceDark,false);Place(bar,new Vector2(.985f,.015f),new Vector2(.998f,.985f));
        var handle=Panel("Handle",bar,ChimeraUITheme.Accent,false);Place(handle,Vector2.zero,Vector2.one);handle.GetComponent<Image>().raycastTarget=true;
        var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        var fade=bar.gameObject.AddComponent<ScrollActivityFade>();fade.Scroll=scroll;
        return scroll;
    }
    private static void Place(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
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
