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

        // --- 👇【核心新增】：隐藏主界面进入按钮 ---
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.SetNavigationVisibility(false);

        RefreshHangar();
    }

    public void CloseHangar()
    {
        gameObject.SetActive(false);

        // 👇【只恢复音质，不切换轨道】
        // 这样如果你在事件中打开机库，关闭后依然会播放事件音乐
        MusicManager.Instance?.SetImmersionMode(false);

        // --- ❌ 删除或注释掉以下逻辑 ---
        /* 
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase && !CombatDirector.Instance.IsCombatActive)
        {
            MusicManager.Instance?.SwitchState(MusicState.Map);
        }
        */

        // --- 👇【新增：导航总线】恢复主界面进入按钮 ---
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.SetNavigationVisibility(true);
    }
}