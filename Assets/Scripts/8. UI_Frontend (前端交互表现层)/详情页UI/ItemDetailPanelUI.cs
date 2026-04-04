// --- START OF FILE ItemDetailPanelUI.cs ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;

    [Header("=== 背景与层级 ===")]
    public Image BackgroundImage;
    public Sprite[] LevelBackgrounds = new Sprite[4];
    // 注意：删除了写死的 ChassisBackground，因为现在由图纸自己决定了！

    [Header("=== 左侧：基础图文区 ===")]
    public Image IconImage;
    public TMP_Text NameText;
    public TMP_Text LevelText;
    public TMP_Text DescriptionText;

    [System.Serializable]
    public struct StatIconMapping
    {
        public StatType Type;
        public Sprite Icon;
    }

    [Header("=== 右侧：属性池配置 ===")]
    public List<StatIconMapping> StatIcons;
    public Transform StatsGridRoot;
    public GameObject StatEntryPrefab;

    [Header("=== 右侧：文本补充区 ===")]
    public TMP_Text TacticalRoleText;
    public TMP_Text SpecialMechanicText;

    [Header("=== 底盘专属：分页控制 ===")]
    public GameObject Panel_Stats;
    public GameObject Panel_Sockets;
    public Button ToggleSocketsButton;
    private bool isShowingSockets = false;

    [System.Serializable]
    public struct SocketIconMapping
    {
        public ComponentType Type;
        public Sprite Icon;
    }
    public List<SocketIconMapping> SocketIcons;
    public Sprite GenericSocketIcon;

    public Transform SocketsGridRoot;
    public GameObject SocketEntryPrefab;

    private void Awake()
    {
        Instance = this;
        if (ToggleSocketsButton != null) ToggleSocketsButton.onClick.AddListener(ToggleChassisTab);
        HidePanel();
    }

    public void HidePanel() { gameObject.SetActive(false); }

    public void ShowComponentDetail(InstancedComponent instance)
    {
        if (instance == null || instance.BaseData == null) return;
        gameObject.SetActive(true);

        var data = instance.BaseData;
        var currentLvData = data.GetLevelData(instance.CurrentLevel);

        int bgIndex = Mathf.Clamp(instance.CurrentLevel - 1, 0, LevelBackgrounds.Length - 1);
        if (BackgroundImage != null && LevelBackgrounds[bgIndex] != null)
            BackgroundImage.sprite = LevelBackgrounds[bgIndex];

        if (ToggleSocketsButton != null) ToggleSocketsButton.gameObject.SetActive(false);
        if (Panel_Sockets != null) Panel_Sockets.SetActive(false);
        if (Panel_Stats != null) Panel_Stats.SetActive(true);

        IconImage.sprite = data.ComponentIcon;
        IconImage.SetNativeSize();
        NameText.text = data.ComponentName;
        LevelText.text = $"Lv.{instance.CurrentLevel}";

        int scrapVal = currentLvData != null ? currentLvData.ScrapValue : 5;
        DescriptionText.text = $"{data.Description}\n\n<color=#888888>[回收估值] : {scrapVal} 废料</color>";

        if (TacticalRoleText != null) TacticalRoleText.text = data.TacticalRoleDesc;
        if (SpecialMechanicText != null) SpecialMechanicText.text = currentLvData != null ? currentLvData.SpecialMechanicDesc : "无特殊机制";

        // 呼叫智能渲染管线！
        RenderIntelligentStatsGrid(currentLvData != null ? currentLvData.Stats : new List<StatEntry>());
    }

    public void ShowChassisDetail(ChassisDataSO data)
    {
        if (data == null) return;
        gameObject.SetActive(true);

        // 👇【核心修复 2】：读取该底盘专属的背景图！
        if (BackgroundImage != null && data.DetailBackgroundSprite != null)
            BackgroundImage.sprite = data.DetailBackgroundSprite;

        isShowingSockets = false;
        if (ToggleSocketsButton != null) ToggleSocketsButton.gameObject.SetActive(true);
        if (Panel_Stats != null) Panel_Stats.SetActive(true);
        if (Panel_Sockets != null) Panel_Sockets.SetActive(false);

        IconImage.sprite = data.ChassisSprite;
        IconImage.SetNativeSize();
        NameText.text = data.ChassisName;
        LevelText.text = "";

        DescriptionText.text = $"{data.Description}\n\n<color=#888888>[回收估值] : {data.ScrapValue} 废料</color>";

        if (TacticalRoleText != null) TacticalRoleText.text = "载具底盘";
        if (SpecialMechanicText != null) SpecialMechanicText.text = data.SpecialMechanicDesc;

        // 呼叫智能渲染管线！
        RenderIntelligentStatsGrid(data.BaseStats);
        RenderSocketsGrid(data.Sockets);
    }

    // ==========================================
    // 🧠 核心修复 3：智能属性合并与剔除管线
    // ==========================================
    private void RenderIntelligentStatsGrid(List<StatEntry> stats)
    {
        if (StatsGridRoot == null || StatEntryPrefab == null) return;

        foreach (Transform child in StatsGridRoot) Destroy(child.gameObject);

        if (stats == null || stats.Count == 0) return;

        // 1. 提取出所有需要合并的成对数据，并将其从常规列表中移除
        float minDmg = 0, maxDmg = 0, minRng = 0, maxRng = 0;
        bool hasDmg = false, hasRng = false;

        var normalStats = new List<StatEntry>();

        foreach (var stat in stats)
        {
            if (stat.Value == 0) continue; // 绝对过滤掉 0 值的无效属性！

            if (stat.StatID == StatType.MinDamage) { minDmg = stat.Value; hasDmg = true; }
            else if (stat.StatID == StatType.MaxDamage) { maxDmg = stat.Value; hasDmg = true; }
            else if (stat.StatID == StatType.MinRange) { minRng = stat.Value; hasRng = true; }
            else if (stat.StatID == StatType.MaxRange) { maxRng = stat.Value; hasRng = true; }
            else
            {
                normalStats.Add(stat); // 其他属性原样保留
            }
        }

        // 2. 渲染合并后的【伤害区间】
        if (hasDmg)
        {
            string dmgText = (minDmg == maxDmg) ? $"{maxDmg}" : $"{minDmg} ~ {maxDmg}";
            CreateStatEntry(StatType.MaxDamage, dmgText); // 用 MaxDamage 的图标代表“攻击力”
        }

        // 3. 渲染合并后的【射程区间】
        if (hasRng)
        {
            string rngText = (minRng == 0) ? $"{maxRng}" : $"{minRng} ~ {maxRng}";
            CreateStatEntry(StatType.MaxRange, rngText); // 用 MaxRange 的图标代表“射程”
        }

        // 4. 渲染剩余的常规单项属性 (如耗电、攻速、暴击)
        foreach (var stat in normalStats)
        {
            string valText = "";
            if (stat.StatID == StatType.CriticalChance) valText = $"+{stat.Value:P0}"; // 暴击转百分比
            else if (stat.StatID == StatType.AttackSpeed) valText = $"{stat.Value}";  // 攻速不加 + 号
            else valText = $"+{stat.Value}"; // 护甲、血量加 + 号

            CreateStatEntry(stat.StatID, valText);
        }
    }

    // 生成单个条目的辅助方法
    private void CreateStatEntry(StatType type, string textValue)
    {
        GameObject entryObj = Instantiate(StatEntryPrefab, StatsGridRoot);
        Image iconImg = entryObj.transform.Find("Icon").GetComponent<Image>();
        TMP_Text valTxt = entryObj.transform.Find("Value").GetComponent<TMP_Text>();

        // 映射图标
        var mapping = StatIcons.FirstOrDefault(x => x.Type == type);
        if (mapping.Icon != null)
        {
            iconImg.sprite = mapping.Icon;
            iconImg.gameObject.SetActive(true);
        }
        else
        {
            iconImg.gameObject.SetActive(false);
        }

        valTxt.text = textValue;
    }

    // ==========================================
    // 渲染工具：底盘专属接口列表
    // ==========================================
    private void RenderSocketsGrid(List<SlotDefinition> sockets)
    {
        if (SocketsGridRoot == null || SocketEntryPrefab == null) return;

        foreach (Transform child in SocketsGridRoot) Destroy(child.gameObject);

        // 如果底盘图纸里完全没配插槽，安全退出
        if (sockets == null || sockets.Count == 0) return;

        foreach (var slot in sockets)
        {
            if (slot == null) continue;

            GameObject entryObj = Instantiate(SocketEntryPrefab, SocketsGridRoot);

            // 1. 绝对安全的寻找 Icon 节点
            Transform iconTrans = entryObj.transform.Find("Icon");
            if (iconTrans == null)
            {
                Debug.LogError("【UI 报错】SocketEntryPrefab 预制体下面找不到叫 'Icon' 的子节点！请检查预制体名字。");
                continue;
            }
            Image iconImg = iconTrans.GetComponent<Image>();

            // 2. 绝对安全的寻找 Name 节点
            Transform nameTrans = entryObj.transform.Find("Name");
            if (nameTrans == null)
            {
                Debug.LogError("【UI 报错】SocketEntryPrefab 预制体下面找不到叫 'Name' 的子节点！(注意首字母大写)");
                continue;
            }
            TMP_Text nameTxt = nameTrans.GetComponent<TMP_Text>();

            // 3. 绝对安全的读取图纸里的 AllowedTypes
            if (slot.AllowedTypes == null)
            {
                Debug.LogWarning($"【数据警告】底盘插槽 [{slot.SlotName}] 没有配置任何 AllowedTypes！跳过显示。");
                continue;
            }

            int allowedCount = slot.AllowedTypes.Count;
            if (allowedCount == 0) continue;

            // 4. 正常渲染图标与文本
            if (allowedCount == 1)
            {
                // 如果在 Inspector 里没配 SocketIcons，或者没找到对应的，做个兜底
                var mapping = SocketIcons != null ? SocketIcons.FirstOrDefault(x => x.Type == slot.AllowedTypes[0]) : default;

                iconImg.sprite = mapping.Icon != null ? mapping.Icon : GenericSocketIcon;
                nameTxt.text = TranslateComponentType(slot.AllowedTypes[0]);
            }
            else
            {
                iconImg.sprite = GenericSocketIcon;
                nameTxt.text = "通用接口";
            }
        }
    }

    private void ToggleChassisTab()
    {
        isShowingSockets = !isShowingSockets;
        Panel_Stats.SetActive(!isShowingSockets);
        Panel_Sockets.SetActive(isShowingSockets);

        TMP_Text btnTxt = ToggleSocketsButton.GetComponentInChildren<TMP_Text>();
        if (btnTxt != null) btnTxt.text = isShowingSockets ? "查看机体属性" : "查看接口规格";
    }

    private string TranslateComponentType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心";
            case ComponentType.Weapon: return "武器";
            case ComponentType.Support: return "辅助";
            case ComponentType.Factory: return "工厂";
            case ComponentType.Movement: return "移动";
            default: return "未知";
        }
    }
}