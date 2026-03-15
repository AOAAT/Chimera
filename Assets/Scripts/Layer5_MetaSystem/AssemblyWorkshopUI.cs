using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssemblyWorkshopUI : MonoBehaviour
{
    public static AssemblyWorkshopUI Instance;

    [Header("=== 核心状态机数据 ===")]
    private SavedUnitProfile currentEditingProfile; // 当前正在编辑的机甲档案
    private bool isCreatingNew = false; // 是否是“从零开始”的新建模式

    [Header("=== 左右分层 UI 面板 ===")]
    public GameObject LeftStatsPanel;     // 左侧属性展示区 (HP, AP, 雷达图)
    public GameObject CenterPreviewArea;  // 中央机甲预览区
    public GameObject RightInventoryPanel;// 右侧弹出的仓库面板 (后续实现)

    [Header("=== 中央预览区 UI 绑定 ===")]
    public GameObject GhostChassisPrompt; // 幽灵态：提示“点击安装底盘”的巨大虚线框按钮
    public Transform ChassisVisualRoot;   // 实体态：用来生成底盘和零件图像的父节点

    [Header("=== 左侧属性区 UI 绑定 ===")]
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text PowerText;
    public TMP_Text UnitNameText;

    [Header("=== 视觉与排版控制 ===")]
    public float WorldToUIMultiplier = 100f; // 坐标放大系数，跟机库里的一样
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false); // 默认隐藏装配车间
    }

    // ==========================================
    // 入口 1：从机库点击 "+" 号进来 (幽灵态)
    // ==========================================
    public void OpenEmptyWorkshop()
    {
        gameObject.SetActive(true);
        currentEditingProfile = null;
        isCreatingNew = true;

        RefreshWorkshopState();
        Debug.Log("【车间开启】欢迎来到空车间！请先安装底盘。");
    }

    // ==========================================
    // 入口 2：从机库点击已有单位进来 (实体态/查看修改)
    // ==========================================
    public void OpenWorkshopWithUnit(SavedUnitProfile unitProfile)
    {
        gameObject.SetActive(true);
        currentEditingProfile = unitProfile;
        isCreatingNew = false;

        RefreshWorkshopState();
        Debug.Log($"【车间开启】正在检修机甲: {unitProfile.UnitName}");
    }

    // ==========================================
    // 核心心流：刷新车间表现
    // ==========================================
    private void RefreshWorkshopState()
    {
        // 先把右侧仓库关掉，保持视野干净
        RightInventoryPanel.SetActive(false);

        if (currentEditingProfile == null)
        {
            // 幽灵态：什么都没有，只能点虚线框
            GhostChassisPrompt.SetActive(true);
            ChassisVisualRoot.gameObject.SetActive(false);

            // 属性归零
            HPText.text = "HP: --";
            APText.text = "AP: --";
            PowerText.text = "耗电: --";
            UnitNameText.text = "等待底盘接入...";
        }
        else
        {
            // 实体态：底盘已就位，开放全部装配功能
            GhostChassisPrompt.SetActive(false);
            ChassisVisualRoot.gameObject.SetActive(true);

            // 1. 刷新左侧数值面板
            HPText.text = $"HP: {currentEditingProfile.CurrentHP}";
            APText.text = $"AP: {currentEditingProfile.CurrentAP}";
            // TODO: 调用与 HangarSlotUI 里一样的计算耗电量逻辑
            UnitNameText.text = currentEditingProfile.UnitName;

            // 2. 渲染中央机甲与隐式插槽按钮
            RenderMechAndSockets();
        }
    }

    // ==========================================
    // 玩家行为：点击了中央的“幽灵虚线框”
    // ==========================================
    public void OnClickGhostChassis()
    {
        Debug.Log("【交互】玩家请求安装底盘！呼出右侧仓库...");

        // 去大管家那里，把【没有被占用】的底盘实体筛选出来！
        List<InstancedChassis> availableChassis = PlayerInventoryManager.Instance.ChassisInventory
            .FindAll(c => !c.IsEquipped);

        if (availableChassis.Count == 0)
        {
            Debug.LogWarning("【库存告急】仓库里没有任何闲置的底盘！");
            return;
        }

        // 【终极重构】：不再自动代选，而是让右侧面板真正把列表刷出来！
        RightInventoryPanelUI.Instance.OpenForChassisSelection(availableChassis, OnChassisSelectedFromInventory);
    }
    // ==========================================
    // 回调枢纽：当玩家在右侧仓库点击了某个底盘后触发
    // ==========================================
    public void OnChassisSelectedFromInventory(InstancedChassis selectedChassis)
    {
        Debug.Log($"【组装开始】底盘实体 [{selectedChassis.BaseData.ChassisName}] 落地！");

        // 1. 消耗这个实体，生成机甲档案！
        currentEditingProfile = new SavedUnitProfile(selectedChassis, "特制原型机");

        // 2. 核心：将这个底盘实体标记为“已被这台机甲占用”！
        selectedChassis.EquippedUnitID = currentEditingProfile.UnitID;
        isCreatingNew = true;

        // 3. 隐藏右侧仓库，保持屏幕干净
        RightInventoryPanel.SetActive(false);

        // 4. 核心大招：刷新车间状态 (从幽灵态 -> 实体态)
        RefreshWorkshopState();
    }

    // ==========================================
    // 渲染机甲实体与生成隐式交互按钮 (核心难点预留)
    // ==========================================
    // ==========================================
    // 核心渲染：动态生成底盘与插槽按钮
    // ==========================================
    private void RenderMechAndSockets()
    {
        // 1. 清理旧的视觉表现 (防止来回切换时图片重叠)
        foreach (Transform child in ChassisVisualRoot)
        {
            Destroy(child.gameObject);
        }

        // 2. 生成底盘基座
        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(ChassisVisualRoot, false);

        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = currentEditingProfile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize(); // 恢复原始原画尺寸

        // 3. 极其硬核的环节：按照图纸动态生成“插槽按钮”！
        for (int i = 0; i < currentEditingProfile.ChassisData.Sockets.Count; i++)
        {
            var slotDef = currentEditingProfile.ChassisData.Sockets[i];
            int slotIndex = i; // 闭包防坑，必须把索引存下来

            // 创建插槽的空壳
            GameObject slotObj = new GameObject($"UI_Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);

            // 给插槽加个半透明底色，方便玩家知道点哪里 (测试阶段用)
            Image slotImg = slotObj.AddComponent<Image>();
            slotImg.color = new Color(1f, 1f, 1f, 0.3f); // 半透明的白色方块
            slotImg.rectTransform.sizeDelta = new Vector2(60, 60); // 插槽按钮的大小

            // 【坐标系转换】：把图纸里的世界坐标放大成 UI 坐标
            slotImg.rectTransform.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            // 还原旋转
            slotImg.rectTransform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // 给插槽装上真正的交互灵魂：Button
            Button slotBtn = slotObj.AddComponent<Button>();

            // 当玩家点下这个半透明方块时，呼叫 OnSlotClicked！
            slotBtn.onClick.AddListener(() => OnSlotClicked(slotIndex));

            // TODO: 未来如果这个插槽上已经装了零件，我们还要在这里叠一层零件的图片！
        }
    }

    // ==========================================
    // 玩家行为：点击了某个具体的零件插槽！
    // ==========================================
    private void OnSlotClicked(int slotIndex)
    {
        var slotDef = currentEditingProfile.ChassisData.Sockets[slotIndex];
        Debug.Log($"【车间交互】玩家点击了插槽 [{slotDef.SlotName}]，索引: {slotIndex}！");

        // 接下来我们要呼出右侧仓库，并且极其严格地只显示“符合该插槽类型”的闲置零件！
        // TODO: 通知 RightInventoryPanelUI 展示零件
    }

    // ==========================================
    // 退出车间拦截器 (防呆设计)
    // ==========================================
    // ==========================================
    // 退出车间拦截器 (防呆设计 + 彻底消除CS0414警告)
    // ==========================================
    public void TryExitWorkshop()
    {
        // 这里就是 isCreatingNew 大显身手的地方！
        if (isCreatingNew)
        {
            Debug.Log("【保存逻辑】玩家正在新建机甲！即将执行[新建档案]并占用一个新的车库槽位...");
            // TODO: 把 currentEditingProfile 塞进 PlayerInventoryManager 的 HangarUnits 列表里
        }
        else
        {
            Debug.Log("【保存逻辑】玩家正在修改已有机甲！即将执行[覆盖原档案]操作...");
            // TODO: 因为传进来的是引用，数据其实已经实时修改了，这里可能只需要存个盘
        }

        // 退出车间，回到机库
        gameObject.SetActive(false);
        HangarMenuUI.Instance.gameObject.SetActive(true);
        HangarMenuUI.Instance.RefreshHangar();
    }
}