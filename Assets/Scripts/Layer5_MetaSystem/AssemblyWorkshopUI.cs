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
        Debug.Log("【交互】玩家请求选择底盘！正在呼出右侧仓库...");

        // 呼出右侧仓库，并传入极其严苛的过滤指令：【只看底盘】
        RightInventoryPanel.SetActive(true);
        // TODO: 通知 Inventory UI 只显示底盘列表
    }

    // ==========================================
    // 渲染机甲实体与生成隐式交互按钮 (核心难点预留)
    // ==========================================
    private void RenderMechAndSockets()
    {
        // 这里的逻辑会非常精彩！我们会读取图纸里的插槽坐标，
        // 在 UI 上动态生成透明的 Button。点哪里，就能改装哪里的零件！
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