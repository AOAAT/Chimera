using System.Collections.Generic;
using UnityEngine;

public class HangarMenuUI : MonoBehaviour
{
    public static HangarMenuUI Instance;

    [Header("=== UI 引用 ===")]
    public Transform SlotGridParent;
    public HangarSlotUI SlotPrefab;

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
        // 现在这是一个长度必定为 8 的数组
        var playerUnits = PlayerInventoryManager.Instance.HangarUnits;

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            // 【极其优雅】：索引 i 完美对应车位号！
            // 把车位号 i 一起传给格子，让格子记住自己是几号车位！
            spawnedSlots[i].RefreshSlot(i, playerUnits[i]);
        }
    }

    // ==========================================
    // 导航枢纽：引出下一步的装配功能
    // ==========================================
    // 【修改】：点击新建时，必须把车位号传给车间！
    public void TriggerCreateNewUnit(int slotIndex)
    {
        gameObject.SetActive(false);
        // 这里未来需要让 AssemblyWorkshopUI 也接收这个 slotIndex
        // 今天咱们先把参数传过去：
        AssemblyWorkshopUI.Instance.OpenEmptyWorkshop(slotIndex); 
        Debug.Log($"【准备新建】长官，我们将在 {slotIndex} 号车位为您建造新机甲！");
    }
    // 【修改】增加 int slotIndex 参数
    public void TriggerOpenUnitDetail(int slotIndex, SavedUnitProfile profile)
    {
        // 传递给详情页
        UnitDetailPanelUI.Instance.OpenDetail(slotIndex, profile);
    }

    public void OpenHangar()
    {
        gameObject.SetActive(true);
        MusicManager.Instance?.SetImmersionMode(true);
        RefreshHangar();
    }

    public void CloseHangar()
    {
        gameObject.SetActive(false);

        // --- 👇【修改】：关闭机库时，只负责恢复音质 ---
        MusicManager.Instance?.SetImmersionMode(false);

        // 只有在非战斗环境下（即大地图环境下），才考虑切回地图音乐
        // 如果在战斗房间里，这里什么都不做，继续播当前的 Combat BGM
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase && !CombatDirector.Instance.IsCombatActive)
        {
            MusicManager.Instance?.SwitchState(MusicState.Map);
        }
    }
}