using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProductionTaskUIItem : MonoBehaviour
{
    public Image ItemIcon;
    public TMP_Text NameText;
    public TMP_Text TimeText;
    public Slider ProgressSlider;
    public GameObject PauseOverlay;
    public Image PlayPauseButtonIcon;

    public Sprite PlaySprite;
    public Sprite PauseSprite;
    [SerializeField] private TMP_Text pauseActionLabel;

    [SerializeField] private TMP_Text reasonText;
    private FactoryBuilding factory;
    private float nextStatusRefresh;
    public ProductionStatusView CurrentStatus { get; private set; }
    private ProductionTask bindedTask;
    private System.Action onCancel;
    public ProductionTask BindedTask => bindedTask; // 🌟 暴露属性
    public void Initialize(ProductionTask task, System.Action cancelCallback, FactoryBuilding owner = null)
    {
        bindedTask = task;
        factory = owner;
        onCancel = cancelCallback;
        RefreshStatus();
        ItemHoverTarget.Bind(gameObject, () => {
            var status = ProductionStatusView.Describe(bindedTask,factory,LogisticsManager.Instance);
            return new ItemHoverContent { RefreshInterval=.5f, Title=bindedTask.ItemName,Icon=bindedTask.Icon,Subtitle=status.Label,
                Body="<b>当前状态</b>\n"+status.Details+"\n\n<b>生产进度</b>\n"+bindedTask.NormalizedProgress.ToString("P0")+"\n可拖动左侧物品图标调整队列顺序。" };
        }, NameText != null ? NameText.font : null);

        if (NameText != null) NameText.text = task.ItemName;
        if (ItemIcon != null) ItemIcon.sprite = task.Icon;
        if (TimeText != null)
        {
            TimeText.enableAutoSizing = true;
            TimeText.fontSizeMin = 12f;
            TimeText.fontSizeMax = 16f;
            TimeText.enableWordWrapping = false;
            TimeText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void Update()
    {
        if (bindedTask == null) return;

        if (ProgressSlider != null) ProgressSlider.value = bindedTask.NormalizedProgress;
        if(Time.unscaledTime>=nextStatusRefresh)
        { nextStatusRefresh=Time.unscaledTime+.25f;RefreshStatus(); }

        // 🌟 视觉同步
        if (PauseOverlay != null) PauseOverlay.SetActive(bindedTask.IsPaused);
        if (pauseActionLabel != null) pauseActionLabel.text = bindedTask.IsPaused ? "继续" : "暂停";
        else if (PlayPauseButtonIcon != null)
            PlayPauseButtonIcon.sprite = bindedTask.IsPaused ? PlaySprite : PauseSprite;
    }

    public void RefreshStatus()
    {
        if(bindedTask==null)return;
        CurrentStatus=ProductionStatusView.Describe(bindedTask,factory,LogisticsManager.Instance);
        if(TimeText!=null){TimeText.text=CurrentStatus.Label;TimeText.color=CurrentStatus.Color;}
        if(reasonText!=null)reasonText.text=CurrentStatus.Hint;
        if(pauseActionLabel!=null)pauseActionLabel.text=bindedTask.IsPaused?"继续":"暂停";
    }

    public void PrepareEditorLayout()
    {
        var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 82; layout.flexibleWidth = 1;
        var background = GetComponent<Image>();
        if(background != null) ChimeraUITheme.StyleSurface(background,ChimeraUITheme.Surface);
        if(ItemIcon != null) { Place(ItemIcon.rectTransform,.015f,.12f,.095f,.88f); ItemIcon.preserveAspect = true; ItemIcon.raycastTarget = false; ItemIcon.color = Color.white; }
        var dragHandle = transform.Find("Handle") as RectTransform;
        if(dragHandle != null)
        {
            Place(dragHandle,.015f,.12f,.095f,.88f);
            var handleImage = dragHandle.GetComponent<Image>();
            if(handleImage != null) { UIThemeBinding.Bind(handleImage,UIThemeRole.None,false,false); handleImage.color = Color.clear; }
        }
        if(NameText != null)
        {
            Place(NameText.rectTransform,.115f,.60f,.49f,.96f);
            NameText.fontSize = 18; NameText.enableAutoSizing = true; NameText.fontSizeMin = 14; NameText.fontSizeMax = 18;
            NameText.enableWordWrapping = false; NameText.overflowMode = TextOverflowModes.Ellipsis;
            NameText.raycastTarget = false;
        }
        if(TimeText != null)
        {
            Place(TimeText.rectTransform,.50f,.60f,.775f,.96f);
            UIThemeBinding.Bind(TimeText,UIThemeRole.None,false,false);
            TimeText.fontSize = 16; TimeText.alignment = TextAlignmentOptions.MidlineRight; TimeText.raycastTarget = false;
        }
        if(reasonText==null)
        {
            reasonText=new GameObject("ProductionReason",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            reasonText.transform.SetParent(transform,false); reasonText.font=NameText!=null?NameText.font:TMP_Settings.defaultFontAsset;
        }
        Place(reasonText.rectTransform,.115f,.24f,.775f,.60f);
        reasonText.fontSize=15;reasonText.enableAutoSizing=false;reasonText.enableWordWrapping=false;
        reasonText.overflowMode=TextOverflowModes.Ellipsis;reasonText.raycastTarget=false;
        reasonText.text="等待空闲生产线；悬停查看原因与处理建议。";
        UIThemeBinding.Bind(reasonText,UIThemeRole.SecondaryText);
        if(ProgressSlider != null)
        {
            Place((RectTransform)ProgressSlider.transform,.115f,.10f,.775f,.19f);
            ProgressSlider.interactable = false;
            if(ProgressSlider.handleRect != null) ProgressSlider.handleRect.gameObject.SetActive(false);
            if(ProgressSlider.fillRect != null)
            {
                var area = ProgressSlider.fillRect.parent as RectTransform;
                if(area != null) { area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero; }
                ProgressSlider.fillRect.offsetMin = ProgressSlider.fillRect.offsetMax = Vector2.zero;
            }
            ChimeraUITheme.StyleSlider(ProgressSlider);
        }
        foreach(var button in GetComponentsInChildren<Button>(true))
        {
            bool pause = false, cancel = false;
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
            {
                pause |= button.onClick.GetPersistentMethodName(i) == "OnClickTogglePause";
                cancel |= button.onClick.GetPersistentMethodName(i) == "OnClickCancel";
            }
            if(!pause && !cancel) continue;
            Place((RectTransform)button.transform,pause ? .80f : .895f,.22f,pause ? .885f : .98f,.78f);
            var image = button.targetGraphic as Image;
            // One-time migration of the former shield-shaped pause/cancel controls.
            if(image != null && image.GetComponent<UIThemeBinding>() == null) { image.sprite = null; image.overrideSprite = null; }
            UIThemeBinding.Bind(image,cancel ? UIThemeRole.DangerButton : UIThemeRole.Button,true);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if(label == null)
            {
                label = new GameObject("ActionLabel",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(button.transform,false); label.font = NameText != null ? NameText.font : TMP_Settings.defaultFontAsset;
            }
            Place(label.rectTransform,0,0,1,1); label.text = pause ? "暂停" : "取消";
            label.gameObject.SetActive(true); label.enabled = true;
            label.fontSize = 15; label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 15;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            UIThemeBinding.Bind(label,cancel ? UIThemeRole.OnAccent : UIThemeRole.PrimaryText);
            if(pause) pauseActionLabel = label;
        }
        if(PauseOverlay != null)
        {
            // State is communicated in text; a full-row overlay would dim controls and obscure progress.
            foreach(var graphic in PauseOverlay.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
        }
    }
    private static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
    {
        rect.anchorMin = new Vector2(x0,y0); rect.anchorMax = new Vector2(x1,y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }

    // 🌟 诊断接口：点击暂停
    public void OnClickTogglePause()
    {
        if (bindedTask == null) return;

        bindedTask.IsPaused = !bindedTask.IsPaused;
        RefreshStatus();
        Debug.Log($"<color=yellow>【UI交互】</color> 任务 {bindedTask.ItemName} 暂停状态变为: {bindedTask.IsPaused}");
    }

    // 🌟 诊断接口：点击取消
    public void OnClickCancel()
    {
        Debug.Log($"<color=red>【UI交互】</color> 请求取消任务: {(bindedTask != null ? bindedTask.ItemName : "NULL")}");
        onCancel?.Invoke();
    }
}
