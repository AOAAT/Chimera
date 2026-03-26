using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneratedReward
{
    public RewardEntry Config;
    public bool IsClaimed = false;

    public InstancedChassis SingleChassis;
    public InstancedComponent SingleComponent;

    public List<InstancedChassis> DraftChassisList = new List<InstancedChassis>();
    public List<InstancedComponent> DraftComponentList = new List<InstancedComponent>();
}

public class RewardDirector : MonoBehaviour
{
    public static RewardDirector Instance { get; private set; }

    [Header("=== 1. 总清单界面 (Reward Hub) ===")]
    public GameObject HubPanel;
    public Transform HubContentRoot;
    public RewardEntryUI HubEntryPrefab;
    public Button LeaveHubButton;

    [Header("=== 2. 单选界面 (Single Reward) ===")]
    public GameObject SinglePanel;
    public Transform SingleSlotRoot;
    public Button SingleAcceptButton;
    public Button SingleDiscardButton;

    [Header("=== 3. 三选一界面 (Draft Choice) ===")]
    public GameObject DraftPanel;
    public Transform DraftSlotRoot;
    public Button DraftAcceptButton;
    public Button DraftDiscardButton;

    [Header("=== 依赖的格子预制体 ===")]
    public InventoryItemSlotUI ItemSlotPrefab;

    private List<GeneratedReward> currentRewards = new List<GeneratedReward>();
    private int activeRewardIndex = -1;

    private List<InventoryItemSlotUI> spawnedDraftSlots = new List<InventoryItemSlotUI>();
    private int selectedDraftItemIndex = -1;
    private InventoryItemSlotUI spawnedSingleSlot;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        if (LeaveHubButton != null) LeaveHubButton.onClick.AddListener(FinishAndLeave);

        if (SingleAcceptButton != null) SingleAcceptButton.onClick.AddListener(ConfirmSingleReward);
        if (SingleDiscardButton != null) SingleDiscardButton.onClick.AddListener(CloseSubPanels);

        if (DraftAcceptButton != null) DraftAcceptButton.onClick.AddListener(ConfirmDraftReward);
        if (DraftDiscardButton != null) DraftDiscardButton.onClick.AddListener(CloseSubPanels);

        CloseAllPanels();
    }

    private void CloseAllPanels()
    {
        if (HubPanel != null) HubPanel.SetActive(false);
        CloseSubPanels();
    }

    private void CloseSubPanels()
    {
        if (SinglePanel != null) SinglePanel.SetActive(false);
        if (DraftPanel != null) DraftPanel.SetActive(false);
        activeRewardIndex = -1;
    }

    public void GenerateAndShowRewards(LootTableSO lootTable)
    {
        currentRewards.Clear();
        CloseAllPanels();

        if (lootTable == null || lootTable.GuaranteedDrops.Count == 0)
        {
            Debug.LogWarning("【战利品】没有掉落物，直接返回地图！");
            FinishAndLeave();
            return;
        }

        foreach (var entry in lootTable.GuaranteedDrops)
        {
            GeneratedReward gen = new GeneratedReward { Config = entry, IsClaimed = false };

            if (entry.Category == RewardCategory.RandomBlindBox)
            {
                RollItemsForReward(gen, 1);
            }
            else if (entry.Category == RewardCategory.DraftChoice)
            {
                RollItemsForReward(gen, 3);
            }
            currentRewards.Add(gen);
        }

        RefreshHubUI();
        HubPanel.SetActive(true);
    }

    private void RefreshHubUI()
    {
        foreach (Transform child in HubContentRoot) Destroy(child.gameObject);

        for (int i = 0; i < currentRewards.Count; i++)
        {
            var reward = currentRewards[i];
            RewardEntryUI uiObj = Instantiate(HubEntryPrefab, HubContentRoot);

            string title = "";
            string desc = "";

            if (reward.Config.Category == RewardCategory.Resource)
            {
                // 👇【彻底修复】：调用 ResourceType
                title = $"获取资源: {reward.Config.ResourceType}";
                desc = $"数量: {reward.Config.ResourceAmount}";
            }
            else if (reward.Config.Category == RewardCategory.RandomBlindBox)
            {
                title = "未知的高级图纸";
                desc = "点击开启单抽盲盒";
            }
            else if (reward.Config.Category == RewardCategory.DraftChoice)
            {
                title = "遗迹科技库";
                desc = "从 3 项科技中选取 1 项";
            }

            uiObj.Initialize(i, title, desc, reward.IsClaimed, OnHubEntryClicked);
        }
    }

    private void OnHubEntryClicked(int index)
    {
        activeRewardIndex = index;
        var reward = currentRewards[index];

        if (reward.IsClaimed) return;

        if (reward.Config.Category == RewardCategory.Resource)
        {
            // 👇【彻底修复】：调用 ResourceType
            Debug.Log($"<color=#FFD700>【资源获取】</color> 获得了 {reward.Config.ResourceAmount} 点 {reward.Config.ResourceType}！");
            reward.IsClaimed = true;
            RefreshHubUI();
        }
        else if (reward.Config.Category == RewardCategory.RandomBlindBox)
        {
            OpenSinglePanel(reward);
        }
        else if (reward.Config.Category == RewardCategory.DraftChoice)
        {
            OpenDraftPanel(reward);
        }
    }

    private void OpenSinglePanel(GeneratedReward reward)
    {
        SinglePanel.SetActive(true);
        foreach (Transform child in SingleSlotRoot) Destroy(child.gameObject);

        spawnedSingleSlot = Instantiate(ItemSlotPrefab, SingleSlotRoot);
        spawnedSingleSlot.SetHighlight(false);

        if (reward.SingleChassis != null)
            spawnedSingleSlot.SetupChassis(reward.SingleChassis, null);
        else if (reward.SingleComponent != null)
            spawnedSingleSlot.SetupComponent(reward.SingleComponent, null);
    }

    private void ConfirmSingleReward()
    {
        var reward = currentRewards[activeRewardIndex];

        if (reward.SingleChassis != null) PlayerInventoryManager.Instance.ChassisInventory.Add(reward.SingleChassis);
        if (reward.SingleComponent != null) PlayerInventoryManager.Instance.ComponentInventory.Add(reward.SingleComponent);

        Debug.Log($"<color=#00FF00>【战利品入库】</color> 单抽盲盒已收下！");
        reward.IsClaimed = true;
        CloseSubPanels();
        RefreshHubUI();
    }

    private void OpenDraftPanel(GeneratedReward reward)
    {
        DraftPanel.SetActive(true);
        foreach (Transform child in DraftSlotRoot) Destroy(child.gameObject);
        spawnedDraftSlots.Clear();
        selectedDraftItemIndex = -1;

        DraftAcceptButton.interactable = false;

        for (int i = 0; i < reward.DraftChassisList.Count; i++)
        {
            int captureIndex = spawnedDraftSlots.Count;
            var slot = Instantiate(ItemSlotPrefab, DraftSlotRoot);
            slot.SetupChassis(reward.DraftChassisList[i], (_) => OnDraftSlotClicked(captureIndex));
            slot.SetHighlight(false);
            spawnedDraftSlots.Add(slot);
        }

        for (int i = 0; i < reward.DraftComponentList.Count; i++)
        {
            int captureIndex = spawnedDraftSlots.Count;
            var slot = Instantiate(ItemSlotPrefab, DraftSlotRoot);
            slot.SetupComponent(reward.DraftComponentList[i], (_) => OnDraftSlotClicked(captureIndex));
            slot.SetHighlight(false);
            spawnedDraftSlots.Add(slot);
        }
    }

    private void OnDraftSlotClicked(int index)
    {
        selectedDraftItemIndex = index;
        DraftAcceptButton.interactable = true;

        for (int i = 0; i < spawnedDraftSlots.Count; i++)
        {
            spawnedDraftSlots[i].SetHighlight(i == index);
        }
    }

    private void ConfirmDraftReward()
    {
        if (selectedDraftItemIndex == -1) return;

        var reward = currentRewards[activeRewardIndex];

        int chassisCount = reward.DraftChassisList.Count;
        if (selectedDraftItemIndex < chassisCount)
        {
            var chosenChassis = reward.DraftChassisList[selectedDraftItemIndex];
            PlayerInventoryManager.Instance.ChassisInventory.Add(chosenChassis);
            Debug.Log($"<color=#00FF00>【战利品入库】</color> 获得了底盘: {chosenChassis.BaseData.ChassisName}");
        }
        else
        {
            var chosenComp = reward.DraftComponentList[selectedDraftItemIndex - chassisCount];
            PlayerInventoryManager.Instance.ComponentInventory.Add(chosenComp);
            Debug.Log($"<color=#00FF00>【战利品入库】</color> 获得了组件: {chosenComp.BaseData.ComponentName}");
        }

        reward.IsClaimed = true;
        CloseSubPanels();
        RefreshHubUI();
    }

    private void RollItemsForReward(GeneratedReward gen, int count)
    {
        RewardTargetType targetType = gen.Config.DetermineFinalTargetType();
        ItemRarity targetRarity = gen.Config.RollRarity();

        if (targetType == RewardTargetType.ChassisOnly)
        {
            var pool = PlayerInventoryManager.Instance.AllChassisDatabase.Where(c => c.Rarity == targetRarity).ToList();
            var drawn = pool.OrderBy(x => Guid.NewGuid()).Take(count).ToList();

            if (count == 1 && drawn.Count > 0) gen.SingleChassis = new InstancedChassis(drawn[0]);
            else foreach (var d in drawn) gen.DraftChassisList.Add(new InstancedChassis(d));
        }
        else
        {
            var pool = PlayerInventoryManager.Instance.AllComponentDatabase.Where(c => c.Rarity == targetRarity).ToList();
            var drawn = pool.OrderBy(x => Guid.NewGuid()).Take(count).ToList();

            if (count == 1 && drawn.Count > 0) gen.SingleComponent = new InstancedComponent(drawn[0]);
            else foreach (var d in drawn) gen.DraftComponentList.Add(new InstancedComponent(d));
        }
    }

    private void FinishAndLeave()
    {
        CloseAllPanels();
        Debug.Log("【战利品】清理完毕，班师回朝！");
        CombatDirector.Instance.ExecuteReturnToMap();
    }
}