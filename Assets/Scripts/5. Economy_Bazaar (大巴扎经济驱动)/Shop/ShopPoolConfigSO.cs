using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShopPool", menuName = "Chimera Protocol/3. 宏观控制/商店进货单 (Shop Pool)")]
public class ShopPoolConfigSO : ScriptableObject
{
    [Header("=== 1. 基础权重 ===")]
    public float PoolWeight = 10f;

    [Header("=== 2. 适用环境 (Filter Criteria) ===")]
    public int TargetStage = 1;
    public int MinDepth = 0;
    public int MaxDepth = 15;

    [Header("=== 3. 进货单 (Roster) ===")]
    [Tooltip("商店会从这个列表里随机抽 6 个摆在货架上。")]
    public List<ComponentDataSO> ComponentRoster = new List<ComponentDataSO>();

    [Tooltip("也可以顺便卖几个底盘")]
    public List<ChassisDataSO> ChassisRoster = new List<ChassisDataSO>();

    [Header("=== 4. 经济杠杆 (Economy) ===")]
    [Tooltip("控制抽出组件的星级概率 (1~4级)")]
    public int Weight_Lv1 = 100;
    public int Weight_Lv2 = 0;
    public int Weight_Lv3 = 0;
    public int Weight_Lv4 = 0;

    [Tooltip("每个商品有多大概率打折？(0.0 ~ 1.0)")]
    [Range(0f, 1f)] public float DiscountChance = 0.2f; // 默认 20% 概率打折

    [Tooltip("打折力度 (0.5 代表半价)")]
    [Range(0.1f, 0.9f)] public float DiscountRate = 0.5f;
}