using System.Collections.Generic;
using UnityEngine;

public class HangarMenuUI : MonoBehaviour
{
    public static HangarMenuUI Instance;

    [Header("=== UI 引用 ===")]
    public Transform SlotGridParent; // 挂载了 GridLayoutGroup 的节点
    public HangarSlotUI SlotPrefab;  // 你做好的槽位预制体

    private List<HangarSlotUI> spawnedSlots = new List<HangarSlotUI>();

    private void Awake()
    {
        Instance = this;
        
        gameObject.SetActive(false);
    }

    private void Start()
    {
        InitializeGrid();
        RefreshHangar();
    }

    // ==========================================
    // 初始化：硬核生成 8 个坑位
    // ==========================================
    private void InitializeGrid()
    {
        int maxSlots = PlayerInventoryManager.Instance.MaxUnitSlots;
        for (int i = 0; i < maxSlots; i++)
        {
            var slot = Instantiate(SlotPrefab, SlotGridParent);
            spawnedSlots.Add(slot);
        }
    }

    // ==========================================
    // 刷新大盘：把玩家资产映射到 UI 上
    // ==========================================
    public void RefreshHangar()
    {
        var playerUnits = PlayerInventoryManager.Instance.HangarUnits;

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (i < playerUnits.Count)
            {
                // 这个车位有车
                spawnedSlots[i].RefreshSlot(playerUnits[i]);
            }
            else
            {
                // 这个车位是空的
                spawnedSlots[i].RefreshSlot(null);
            }
        }
    }

    // ==========================================
    // 导航枢纽：引出下一步的装配功能
    // ==========================================
    public void TriggerCreateNewUnit()
    {
        gameObject.SetActive(false);
        // 连招接上！启动装配车间幽灵态！
        AssemblyWorkshopUI.Instance.OpenEmptyWorkshop();
    }

    public void TriggerOpenUnitDetail(SavedUnitProfile profile)
    {
        // 以前是直接粗暴地进入车间：
        // AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(profile); 

        // 现在是优雅地呼出详情面板：
        UnitDetailPanelUI.Instance.OpenDetail(profile);
    }

    // ==========================================
    // 封装入口：打开机库
    // ==========================================
    public void OpenHangar()
    {
        gameObject.SetActive(true);
        RefreshHangar(); // 调用你原本写好的刷新机甲列表的函数
    }

    // ==========================================
    // 封装出口：关闭机库并返回主基地
    // ==========================================
    public void CloseHangar()
    {
        gameObject.SetActive(false);
        // 【核心联动】：关掉机库后，把主基地大厅重新显示出来！
        // MainBaseUI.Instance.gameObject.SetActive(true); 
    }
}