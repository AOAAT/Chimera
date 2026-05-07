using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopDirector : MonoBehaviour
{
    public static ShopDirector Instance;

    [Header("=== 商店配置池 ===")]
    public List<ShopPoolConfigSO> GlobalShopPools = new List<ShopPoolConfigSO>();

    [Header("=== UI 引用 ===")]
    public GameObject ShopPanel;
    public Transform ShelfGridRoot;      // 挂载 GridLayoutGroup (2x3)
    public GameObject ShopItemWidgetPrefab; // 商店商品复合预制体
    public Button LeaveShopButton;

    private MapNodeData currentNodeData;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        LeaveShopButton.onClick.AddListener(LeaveShop);
        ShopPanel.SetActive(false);
    }

    public void EnterShopPhase(MapNodeData nodeData)
    {
        MusicManager.Instance?.SwitchState(MusicState.Shop);
        currentNodeData = nodeData;

        int stage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
        int layer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        var validPools = GlobalShopPools.Where(p => p.TargetStage == stage && layer >= p.MinDepth && layer <= p.MaxDepth).ToList();

        if (validPools.Count == 0)
        {
            Debug.LogWarning("【商店异常】找不到匹配该层数的进货单！直接离开！");
            LeaveShop();
            return;
        }

        var pool = validPools[Random.Range(0, validPools.Count)];
        GenerateShopGoods(pool);
        MusicManager.Instance?.SetImmersionMode(true);
    }

    private void GenerateShopGoods(ShopPoolConfigSO pool)
    {
        ShopPanel.SetActive(true);
        foreach (Transform child in ShelfGridRoot) Destroy(child.gameObject);

        // 凑齐 6 件商品
        int totalItems = 6;
        List<object> goodsToSell = new List<object>();

        // 混抽算法：如果有底盘，给 20% 概率抽底盘，80% 抽组件
        for (int i = 0; i < totalItems; i++)
        {
            if (pool.ChassisRoster.Count > 0 && Random.value < 0.2f)
                goodsToSell.Add(pool.ChassisRoster[Random.Range(0, pool.ChassisRoster.Count)]);
            else if (pool.ComponentRoster.Count > 0)
                goodsToSell.Add(pool.ComponentRoster[Random.Range(0, pool.ComponentRoster.Count)]);
        }

        // 上架摆盘！
        foreach (var good in goodsToSell)
        {
            GameObject widgetObj = Instantiate(ShopItemWidgetPrefab, ShelfGridRoot);

            // 获取你嵌套在里面的那个仓库格子预制体 (InventoryItemSlotUI)
            var slotUI = widgetObj.GetComponentInChildren<InventoryItemSlotUI>();
            var buyBtn = widgetObj.GetComponentInChildren<Button>();
            var priceTxt = buyBtn.GetComponentInChildren<TMP_Text>();

            int originalPrice = 0;
            InstancedComponent compInstance = null;
            InstancedChassis chassisInstance = null;

            // 1. 生成带等级的实体，并查原价
            if (good is ComponentDataSO compSO)
            {
                int lv = RollLevel(compSO, pool);
                compInstance = new InstancedComponent(compSO, lv);
                originalPrice = compSO.GetLevelData(lv).BasePrice;
                slotUI.SetupComponent(compInstance, null); // 商店里点图标不执行操作，只准点下面的购买按钮！
            }
            else if (good is ChassisDataSO chassisSO)
            {
                chassisInstance = new InstancedChassis(chassisSO);
                originalPrice = chassisSO.BasePrice;
                slotUI.SetupChassis(chassisInstance, null);
            }

            // 2. 算折扣！
            bool isDiscounted = Random.value <= pool.DiscountChance;
            int finalPrice = isDiscounted ? Mathf.RoundToInt(originalPrice * pool.DiscountRate) : originalPrice;

            if (isDiscounted) priceTxt.text = $"<color=red><s>{originalPrice}</s></color> <color=#00FF00>{finalPrice} 废料</color>";
            else priceTxt.text = $"{finalPrice} 废料";

            // 3. 闭包绑定购买逻辑
            GameObject capturedWidget = widgetObj;
            buyBtn.onClick.AddListener(() => TryBuyItem(compInstance, chassisInstance, finalPrice, buyBtn, capturedWidget));
        }
    }

    private void TryBuyItem(InstancedComponent comp, InstancedChassis chassis, int price, Button buyBtn, GameObject widget)
    {
        if (GlobalResourceManager.Instance.Materials < price)
        {
            Debug.LogWarning($"【余额不足】需要 {price} 废料，当前只有 {GlobalResourceManager.Instance.Materials}！");
            return;
        }

        // --- 👇【核心新增】：触发飞入动画 ---
        if (JuicyLootEffectManager.Instance != null)
        {
            Sprite iconToFly = null;
            if (comp != null) iconToFly = comp.BaseData.ComponentIcon;
            else if (chassis != null) iconToFly = chassis.BaseData.ChassisSprite;

            // 起点：直接取当前这个商品 UI 槽位（widget）的位置
            Vector3 startPos = widget.transform.position;

            JuicyLootEffectManager.Instance.SpawnFlyEffect(iconToFly, startPos);
        }
        // ----------------------------------

        // 1. 扣钱！
        GlobalResourceManager.Instance.ModifyMaterials(-price);

        // 2. 发货！
        if (comp != null) PlayerInventoryManager.Instance.ComponentInventory.Add(comp);
        if (chassis != null) PlayerInventoryManager.Instance.ChassisInventory.Add(chassis);
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();

        // 3. 买定离手！(Sold Out 视觉表现)
        buyBtn.interactable = false;
        buyBtn.GetComponentInChildren<TMP_Text>().text = "<s>已售罄</s>";

        var canvasGroup = widget.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = widget.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.4f;

        Debug.Log("<color=#FFD700>【交易成功】</color> 银货两讫！");
    }
    private int RollLevel(ComponentDataSO bp, ShopPoolConfigSO pool)
    {
        int w1 = pool.Weight_Lv1; int w2 = pool.Weight_Lv2;
        int w3 = pool.Weight_Lv3; int w4 = pool.Weight_Lv4;
        if (bp.MinDropLevel > 1) w1 = 0;
        if (bp.MinDropLevel > 2) w2 = 0;
        if (bp.MinDropLevel > 3) w3 = 0;
        int total = w1 + w2 + w3 + w4;
        if (total <= 0) return bp.MinDropLevel;
        int roll = Random.Range(0, total);
        if (roll < w1) return 1;
        if (roll < w1 + w2) return 2;
        if (roll < w1 + w2 + w3) return 3;
        return 4;
    }

    private void LeaveShop()
    {
        ShopPanel.SetActive(false);
        MapManager.Instance.OnCombatVictory(currentNodeData); // 复用地图切回逻辑
        MusicManager.Instance?.SetImmersionMode(false);
    }
}