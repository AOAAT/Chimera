using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    public bool IsCombatActive { get; private set; }

    // 👇【核心防呆锁】：向外界（特别是 HangarSlotUI）暴露当前是否允许拖拽机甲
    public bool IsDeploymentPhase { get; private set; }

    [Header("=== UI 引用：战前部署 ===")]
    public GameObject CombatUIPanel;
    public Button StartBattleButton;

    [Header("=== UI 引用：导航栏大锁 ===")]
    public Button NavHangarButton;
    public Button NavWarehouseButton;

    [Header("=== UI 引用：战后结算 ===")]
    public GameObject SettlementPanel;
    public TMP_Text SettlementTitleText;
    public Button ReturnToMapButton;

    [Header("=== 真实世界引用 ===")]
    [Tooltip("战场正中心点")]
    public Transform ArenaCenter;
    [Tooltip("怪物的通用空壳预制体 (必须挂载 EnemyBrain)")]
    public GameObject BaseEnemyPrefab;

    [Tooltip("一张纯白色的 1x1 像素贴图，用于可视化红区")]
    public Sprite WhitePixelSprite;

    [Header("=== 运行时状态监视 (只读) ===")]
    public EncounterLayoutSO CurrentLayout;
    private GameObject forbiddenZonesContainer;

    private MapNodeData currentNodeData;

    private List<DamageReceiver> activeEnemies = new List<DamageReceiver>();
    private List<DamageReceiver> activePlayerUnits = new List<DamageReceiver>();
    private bool isCheckingWinCondition = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (StartBattleButton != null)
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);

        if (ReturnToMapButton != null)
            ReturnToMapButton.onClick.AddListener(OnReturnToMapClicked);

        if (SettlementPanel != null) SettlementPanel.SetActive(false);
    }

    // ==========================================
    // 阶段 1：进入备战部署阶段
    // ==========================================
    public void EnterCombatPhase(MapNodeData nodeData)
    {
        IsCombatActive = false;

        // 👇【核心修复】：进入战斗场景时，正式开启部署权限！
        IsDeploymentPhase = true;

        isCheckingWinCondition = false;
        currentNodeData = nodeData;

        if (CombatUIPanel != null) CombatUIPanel.SetActive(true);
        if (StartBattleButton != null) StartBattleButton.interactable = true;
        if (SettlementPanel != null) SettlementPanel.SetActive(false);

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.OpenHangar();

        if (RunManager.Instance == null)
        {
            Debug.LogError("【致命错误】找不到 RunManager 指挥中枢！无法生成敌人！");
            return;
        }

        CurrentLayout = RunManager.Instance.GetNextEncounterForCurrentNode();
        if (CurrentLayout != null)
        {
            SpawnEnemiesFromLayout();
            GenerateForbiddenZones();
        }
        else
        {
            Debug.LogWarning("【警告】当前节点没有获取到遭遇战图纸！");
        }
    }

    private void SpawnEnemiesFromLayout()
    {
        if (ArenaCenter != null)
        {
            foreach (Transform child in ArenaCenter) Destroy(child.gameObject);
        }

        Vector3 centerPos = ArenaCenter != null ? ArenaCenter.position : Vector3.zero;

        foreach (var spawnData in CurrentLayout.Enemies)
        {
            if (spawnData.EnemyType == null) continue;

            Vector3 spawnPos = centerPos + new Vector3(spawnData.LocalPosition.x, spawnData.LocalPosition.y, 0f);
            GameObject enemyObj = Instantiate(BaseEnemyPrefab, spawnPos, Quaternion.identity, ArenaCenter);
            enemyObj.name = $"[Enemy] {spawnData.EnemyType.EnemyName}";

            EnemyBrain brain = enemyObj.GetComponent<EnemyBrain>();
            if (brain != null) brain.MyData = spawnData.EnemyType;

            SpriteRenderer sr = enemyObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && spawnData.EnemyType.EnemySprite != null)
            {
                sr.sprite = spawnData.EnemyType.EnemySprite;
            }
        }
    }

    private void GenerateForbiddenZones()
    {
        if (forbiddenZonesContainer != null) Destroy(forbiddenZonesContainer);
        forbiddenZonesContainer = new GameObject("[Forbidden Zones]");
        forbiddenZonesContainer.transform.SetParent(this.transform);

        Vector3 centerPos = ArenaCenter != null ? ArenaCenter.position : Vector3.zero;

        int noDeployLayer = LayerMask.NameToLayer("NoDeploy");
        if (noDeployLayer == -1) Debug.LogError("【致命错误】长官！找不到 NoDeploy 图层！");

        foreach (var rect in CurrentLayout.ForbiddenZones)
        {
            GameObject zonePhysicsObj = new GameObject("Zone_Blocker_Physics");
            zonePhysicsObj.transform.SetParent(forbiddenZonesContainer.transform);

            Vector3 zonePos = centerPos + new Vector3(rect.x + rect.width / 2f, rect.y - rect.height / 2f, 0f);
            zonePhysicsObj.transform.position = zonePos;
            zonePhysicsObj.transform.localScale = Vector3.one;

            BoxCollider2D col = zonePhysicsObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(rect.width, rect.height);
            col.isTrigger = true;

            if (noDeployLayer != -1) zonePhysicsObj.layer = noDeployLayer;

            if (WhitePixelSprite != null)
            {
                GameObject visualObj = new GameObject("Zone_Visual");
                visualObj.transform.SetParent(zonePhysicsObj.transform);
                visualObj.transform.localPosition = Vector3.zero;

                SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
                sr.sprite = WhitePixelSprite;

                Vector2 spriteSize = sr.sprite.bounds.size;
                float scaleX = rect.width / spriteSize.x;
                float scaleY = rect.height / spriteSize.y;

                visualObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                sr.color = new Color(1f, 0f, 0f, 0.2f);
                sr.sortingOrder = -5;
            }
        }
    }

    // ==========================================
    // 阶段 2：正式开战 
    // ==========================================
    private void OnBattleStartClicked()
    {
        activeEnemies.Clear();
        activePlayerUnits.Clear();
        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            if (r.isEnemy) activeEnemies.Add(r);
            else activePlayerUnits.Add(r);
        }
        if (activePlayerUnits.Count == 0)
        {
            Debug.LogWarning("【系统警告】长官，您还没有向战场空投任何机甲，禁止开战！");
            return;
        }

        if (StartBattleButton != null) StartBattleButton.interactable = false;
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.CloseHangar();
        if (GlobalWarehouseUI.Instance != null) GlobalWarehouseUI.Instance.CloseWarehouse();
        if (NavHangarButton != null) NavHangarButton.interactable = false;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = false;

        Debug.Log("【战斗导演】引擎轰鸣！发令枪响，全军出击！机库与物品库的大门连同钥匙已全部销毁！");

        if (forbiddenZonesContainer != null)
        {
            forbiddenZonesContainer.SetActive(false);
        }

        IsCombatActive = true;

        // 👇【核心修复】：发令枪响，彻底收回部署权限！现在任何人都不能再从机库往下拖机甲了！
        IsDeploymentPhase = false;

        isCheckingWinCondition = true;
    }

    // ==========================================
    // 阶段 2.5：裁判时刻
    // ==========================================
    private void Update()
    {
        if (!IsCombatActive || !isCheckingWinCondition) return;

        bool allEnemiesDead = true;
        foreach (var e in activeEnemies)
        {
            if (e != null && e.CurrentHP > 0) { allEnemiesDead = false; break; }
        }
        if (allEnemiesDead)
        {
            TriggerSettlement(true);
            return;
        }

        bool allPlayersDead = true;
        foreach (var p in activePlayerUnits)
        {
            if (p != null && p.CurrentHP > 0) { allPlayersDead = false; break; }
        }
        if (allPlayersDead)
        {
            TriggerSettlement(false);
        }
    }

    // ==========================================
    // 阶段 3：吹哨停战
    // ==========================================
    private void TriggerSettlement(bool isVictory)
    {
        IsCombatActive = false;
        isCheckingWinCondition = false;

        // 👇【防呆保险】：防止战斗结束后、回到地图前这个真空期，玩家还能拖拽机甲
        IsDeploymentPhase = false;

        Debug.Log(isVictory ? "【战斗结束】大获全胜！" : "【战斗结束】全军覆没...");

        if (SettlementPanel != null)
        {
            SettlementPanel.SetActive(true);
            if (SettlementTitleText != null)
            {
                SettlementTitleText.text = isVictory ? "战 斗 胜 利" : "任 务 失 败";
                SettlementTitleText.color = isVictory ? Color.green : Color.red;
            }
        }
    }

    // ==========================================
    // 阶段 4：打扫战场，拦截结算，班师回朝
    // ==========================================
    private void OnReturnToMapClicked()
    {
        // 1. 打扫战场与数据回填 (无论输赢都要执行)
        MechUnit2D[] allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var mech in allMechs)
        {
            mech.SyncPostCombatState();
            Destroy(mech.gameObject);
        }
        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            if (r != null) Destroy(r.gameObject);
        }
        Projectile[] allBullets = FindObjectsOfType<Projectile>();
        foreach (var b in allBullets) Destroy(b.gameObject);

        if (forbiddenZonesContainer != null) Destroy(forbiddenZonesContainer);

        // 2. 收起所有的战时/结算UI
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        // 解锁外围导航栏
        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        // 重置玩家机甲的部署状态
        foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
        {
            if (profile != null) profile.IsDeployed = false;
        }

        // 3. 👇【核心拦截流】：赢了去抽卡，输了直接滚！
        bool isVictory = SettlementTitleText != null && SettlementTitleText.text.Contains("胜 利");
        if (isVictory)
        {
            if (RewardDirector.Instance != null && CurrentLayout != null && CurrentLayout.NodeLootTable != null)
            {
                Debug.Log("<color=#FFD700>【战利品拦截】</color> 呼叫战利品导演！请玩家抽卡！");
                RewardDirector.Instance.GenerateAndShowRewards(CurrentLayout.NodeLootTable);
            }
            else
            {
                Debug.LogWarning("【战利品跳过】没有配置掉落表或找不到 RewardDirector，直接返回大地图。");
                ExecuteReturnToMap();
            }
        }
        else
        {
            Debug.LogError("【肉鸽终结】机甲全毁，本次探险结束！请大侠重新来过！");
        }
    }

    public void ExecuteReturnToMap()
    {
        Debug.Log("【战斗导演】系统交接完毕，将指挥权交还给大地图...");
        MapManager.Instance.OnCombatVictory(currentNodeData);
    }

    private void OnDrawGizmos()
    {
        if (ArenaCenter != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(ArenaCenter.position + Vector3.up * 2, ArenaCenter.position + Vector3.down * 2);
            Gizmos.DrawLine(ArenaCenter.position + Vector3.left * 2, ArenaCenter.position + Vector3.right * 2);
            Gizmos.DrawWireSphere(ArenaCenter.position, 0.5f);
        }
    }
}