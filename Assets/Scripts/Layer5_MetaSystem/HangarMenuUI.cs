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

    public void TriggerOpenUnitDetail(SavedUnitProfile unit)
    {
        gameObject.SetActive(false);
        // 连招接上！带入实体数据！
        AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(unit);
    }
}