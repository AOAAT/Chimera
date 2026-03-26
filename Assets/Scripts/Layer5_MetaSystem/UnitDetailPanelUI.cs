using UnityEngine;
using UnityEngine.UI; // 【必须加这一句，为了使用 Image 组件】
using TMPro;

public class UnitDetailPanelUI : MonoBehaviour
{
    public static UnitDetailPanelUI Instance;

    [Header("=== 纯文本数据 UI 绑定 ===")]
    public TMP_Text NameText;
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text PowerText;

    [Header("=== 机甲预览图 UI 绑定 ===")]
    [Tooltip("在这里拖入一个空的RectTransform节点，作为机甲图片的挂载点")]
    public RectTransform UnitVisualContainer;

    [Range(0.1f, 5f)]
    [Tooltip("详情页的机甲通常需要比机库里更大、更霸气，可以在这里调大倍数")]
    public float PreviewScale = 1.5f;
    public float WorldToUIMultiplier = 100f;

    private SavedUnitProfile currentProfile;
    private int currentSlotIndex = -1; // 【新增】接住传过来的车位号

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false); // 默认隐藏
    }

    // ==========================================
    // 从机库接收指令：打开详情页！
    // ==========================================
    public void OpenDetail(int slotIndex, SavedUnitProfile profile)
    {
        currentSlotIndex = slotIndex;
        currentProfile = profile;
        gameObject.SetActive(true);

        // 👇【核心修复】：在详情页当场算出这台机甲的 MaxHP 和总耗电！
        float maxHP = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedHP);
        float totalPower = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);

        foreach (var compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null)
            {
                maxHP += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.AddedHP);
                totalPower += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.PowerCost);
            }
        }

        NameText.text = profile.UnitName;
        // 👇 统一为 RPG 格式：当前血量 / 最大血量！
        HPText.text = $"生命值 (HP): {profile.CurrentHP} / {maxHP}";
        APText.text = $"装甲值 (AP): {profile.CurrentAP}"; // AP在车间外永远是满的，就不写分母了
        PowerText.text = $"机体总耗电: {totalPower}";

        BuildUnitVisual(profile);
    }

    // ==========================================
    // 【核心渲染】完美复用机库的正骨拼装逻辑
    // ==========================================
    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        // 清理旧图
        foreach (Transform child in UnitVisualContainer)
        {
            Destroy(child.gameObject);
        }

        // 应用缩放比例 (详情页大图特写！)
        UnitVisualContainer.localScale = Vector3.one * PreviewScale;

        // 生成底盘基座
        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(UnitVisualContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();

        // 按照插槽数据，把零件一个个完美“焊”上去
        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            string compID = profile.EquippedComponentIDs[i];

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];

            // A. 插槽基座
            GameObject slotObj = new GameObject($"UI_Slot_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // B. Hinge 转轴
            GameObject hingeObj = new GameObject("UI_Hinge");
            hingeObj.transform.SetParent(slotRect, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            // C. Visual 图片 
            GameObject visObj = new GameObject("Sprite_Visual");
            visObj.transform.SetParent(hingeObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();

            // 【核心负号偏移】治愈脱臼！
            compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;

            // 👇👇👇 【新增核心逻辑：给图片注入点击灵魂！】 👇👇👇

            // 1. 动态给这个零件图片挂上一个 Button 组件
            Button compBtn = visObj.AddComponent<Button>();

            // 【极其重要的主程防坑提醒】：闭包陷阱！
            // 因为这是在 for 循环里，必须把当前零件的数据暂存到一个局部变量里，否则所有按钮都会指向最后一个零件！
            ComponentDataSO targetData = comp.BaseData;

            // 2. 用代码给按钮绑定点击事件：召唤物品详情页！
            compBtn.onClick.AddListener(() =>
            {
                // 呼叫咱们刚做好的高级流式标签详情页
                ItemDetailPanelUI.Instance.ShowComponentDetail(targetData);
            });

            // 👆👆👆 ========================================== 👆👆👆
        }
    }


    // ==========================================
    // 按钮功能 1：进入车间改装！
    // ==========================================
    public void OnClickRefit()
    {
        gameObject.SetActive(false);
        HangarMenuUI.Instance.gameObject.SetActive(false);
        // 👇 【核心修复】：把 currentSlotIndex 传给车间！
        AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(currentSlotIndex, currentProfile);


    }

    // ==========================================
    // 按钮功能 2：关闭面板
    // ==========================================
    public void OnClickClose()
    {
        gameObject.SetActive(false);
    }
}