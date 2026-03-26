using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RewardEntry
{
    [Header("1. 这是什么类型的奖励？")]
    public RewardCategory Category;

    [Header("2. 如果是【资源】，发多少？")]
    public StatType ResourceType = StatType.PowerCost;
    public int ResourceAmount = 50;

    [Header("3. 如果是【盲盒/三选一】，发什么？")]
    public RewardTargetType TargetType = RewardTargetType.SmartMix;

    // 👇【核心升级】：当选择 SmartMix 时，底盘和组件的掉落权重比例
    [Header("    └─ [仅 SmartMix 有效] 基础出货权重")]
    [Tooltip("抽出底盘的基础概率权重 (例如: 20)")]
    public int Weight_Chassis = 20;
    [Tooltip("抽出组件的基础概率权重 (例如: 80)")]
    public int Weight_Component = 80;

    [Header("4. 抽卡品质权重 (所见即所得)")]
    public int Weight_Common = 50;
    public int Weight_Uncommon = 30;
    public int Weight_Rare = 15;
    public int Weight_Epic = 5;
    public int Weight_Legendary = 0;

    // ==========================================
    // 🧠 内部智能引擎：决定到底发底盘还是发组件？
    // ==========================================
    public RewardTargetType DetermineFinalTargetType()
    {
        // 如果策划写死了只发其中一种，直接返回！
        if (TargetType == RewardTargetType.ComponentOnly) return RewardTargetType.ComponentOnly;
        if (TargetType == RewardTargetType.ChassisOnly) return RewardTargetType.ChassisOnly;

        // 如果是 SmartMix，进入动态掷骰子环节！
        int currentChassisWeight = Weight_Chassis;
        int currentComponentWeight = Weight_Component;

        // 👇【主策神级需求：动态补底盘机制！】👇
        if (PlayerInventoryManager.Instance != null)
        {
            // 统计玩家目前的“可用底盘数量”
            // 1. 仓库里吃灰的空底盘
            int freeChassisCount = PlayerInventoryManager.Instance.ChassisInventory.FindAll(c => !c.IsEquipped).Count;

            // 2. 机库里已经拼装好的机甲（这也算玩家拥有的底盘资产）
            int deployedChassisCount = 0;
            foreach (var unit in PlayerInventoryManager.Instance.HangarUnits)
            {
                if (unit != null && unit.ChassisData != null) deployedChassisCount++;
            }

            int totalChassisAssets = freeChassisCount + deployedChassisCount;

            // ⚠️ 极其硬核的保底公式 (你可以随时调整数值)：
            // 如果玩家手里一共连 2 个底盘都没有，系统开始“恐慌式发底盘”！
            if (totalChassisAssets <= 2)
            {
                Debug.Log($"<color=#FF8C00>【系统补给触发】</color> 玩家极度缺乏底盘资产 (当前:{totalChassisAssets}台)，底盘掉落权重暴增 300%！");
                currentChassisWeight *= 3;
            }
            // 如果玩家手里底盘泛滥（大于 5 台），系统开始“克制发底盘”！
            else if (totalChassisAssets >= 5)
            {
                Debug.Log($"<color=#00FFFF>【系统干预】</color> 玩家底盘已足够 (当前:{totalChassisAssets}台)，底盘掉落权重削减 50%！");
                currentChassisWeight /= 2;
            }
        }
        // 👆👆👆=================================👆👆👆

        // 经典的轮盘赌算法
        int totalWeight = currentChassisWeight + currentComponentWeight;
        if (totalWeight <= 0) return RewardTargetType.ComponentOnly; // 兜底全给组件

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < currentChassisWeight)
        {
            return RewardTargetType.ChassisOnly; // 骰子停在了底盘区！
        }
        else
        {
            return RewardTargetType.ComponentOnly; // 骰子停在了组件区！
        }
    }

    // 决定这件装备的稀有度
    public ItemRarity RollRarity()
    {
        int totalWeight = Weight_Common + Weight_Uncommon + Weight_Rare + Weight_Epic + Weight_Legendary;
        if (totalWeight <= 0) return ItemRarity.Common;

        int roll = UnityEngine.Random.Range(0, totalWeight);

        if (roll < Weight_Common) return ItemRarity.Common;
        roll -= Weight_Common;
        if (roll < Weight_Uncommon) return ItemRarity.Uncommon;
        roll -= Weight_Uncommon;
        if (roll < Weight_Rare) return ItemRarity.Rare;
        roll -= Weight_Rare;
        if (roll < Weight_Epic) return ItemRarity.Epic;

        return ItemRarity.Legendary;
    }
}

// ==========================================
// 掉落表容器 (挂在不同节点上)
// ==========================================
[CreateAssetMenu(fileName = "NewLootTable", menuName = "Chimera Protocol/Economy/Loot Table (掉落表)")]
public class LootTableSO : ScriptableObject
{
    [Header("=== 战斗结算时，总清单里会出现哪些条目？ ===")]
    public List<RewardEntry> GuaranteedDrops = new List<RewardEntry>();
}