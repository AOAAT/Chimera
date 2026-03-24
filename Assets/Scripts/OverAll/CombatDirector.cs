using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    public bool IsCombatActive { get; private set; }


    [Header("=== UI 引用：战前部署 ===")]
    public GameObject CombatUIPanel;
    public Button StartBattleButton;

    // 👇【新增】：把左侧导航栏的两个“罪魁祸首”按钮拉进来！
    [Header("=== UI 引用：导航栏大锁 ===")]
    public Button NavHangarButton;      // 点击打开机库的那个按钮
    public Button NavWarehouseButton;   // 点击打开仓库的那个按钮

    [Header("=== UI 引用：战后结算 ===")]
    public GameObject SettlementPanel;
    public TMP_Text SettlementTitleText;
    public Button ReturnToMapButton;


    [Header("=== 真实世界引用 ===")]
    public Transform EnemySpawnWorldPoint;
    public GameObject TestEnemyPrefab;

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
        isCheckingWinCondition = false;
        currentNodeData = nodeData;

        if (CombatUIPanel != null) CombatUIPanel.SetActive(true);
        if (StartBattleButton != null) StartBattleButton.interactable = true;
        if (SettlementPanel != null) SettlementPanel.SetActive(false);

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        // 极其贴心地自动为玩家弹开机库面板，方便拖拽
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.OpenHangar();

        SpawnEnemiesForDeployment(nodeData);
    }

    private void SpawnEnemiesForDeployment(MapNodeData nodeData)
    {
        foreach (Transform child in EnemySpawnWorldPoint) Destroy(child.gameObject);

        int enemyCount = (nodeData.NodeType == MapNodeType.Elite) ? 3 : 2;

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject enemy = Instantiate(TestEnemyPrefab, EnemySpawnWorldPoint);
            enemy.transform.localScale = TestEnemyPrefab.transform.localScale;

            Vector2 randomVisualOffset = Random.insideUnitCircle * 1.5f;
            Vector3 finalPos = new Vector3(randomVisualOffset.x, randomVisualOffset.y, 0f);
            enemy.transform.localPosition = finalPos;
        }
    }

    // ==========================================
    // 阶段 2：正式开战
    // ==========================================
    private void OnBattleStartClicked()
    {
        // 1. 【防呆拦截】：开战前清点人数！没下兵不准开战！
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
            return; // 直接拦截！
        }

        // 2. 👇【核心锁定】：最高指令！封锁所有战前交互 UI！
        if (StartBattleButton != null) StartBattleButton.interactable = false;

        // 强制关闭左侧【机库】与【全局仓库】面板，彻底断绝战时作弊念想！
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.CloseHangar();
        if (GlobalWarehouseUI.Instance != null) GlobalWarehouseUI.Instance.CloseWarehouse();

        if (NavHangarButton != null) NavHangarButton.interactable = false;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = false;

        Debug.Log("【战斗导演】引擎轰鸣！发令枪响，全军出击！机库与物品库的大门连同钥匙已全部销毁！");

        // 3. 裁判开始吹哨！AI 激活！
        IsCombatActive = true;
        isCheckingWinCondition = true;
    }

    // ==========================================
    // 阶段 2.5：裁判时刻 (每帧死盯血条)
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
    // 阶段 3：吹哨停战，弹结算面板
    // ==========================================
    private void TriggerSettlement(bool isVictory)
    {
        IsCombatActive = false; // 瞬间锁死全场 AI 和武器
        isCheckingWinCondition = false;

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
    // 阶段 4：打扫战场，班师回朝
    // ==========================================
    private void OnReturnToMapClicked()
    {
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        // 👇👇👇【核心修复 1】：在毁尸灭迹之前，先命令所有玩家机甲把数据写回机库！
        MechUnit2D[] allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var mech in allMechs)
        {
            mech.SyncPostCombatState(); // 抄写数据！
            Destroy(mech.gameObject);   // 抄完再销毁
        }

        // 👇👇👇【核心修复 2】：销毁场上剩下的敌人沙包
        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            if (r != null) Destroy(r.gameObject);
        }

        Projectile[] allBullets = FindObjectsOfType<Projectile>();
        foreach (var b in allBullets) Destroy(b.gameObject);

        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        // 2. 数据归位：重置出战状态
        foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
        {
            if (profile != null) profile.IsDeployed = false;
        }

        // 3. 呼叫地图系统：切回地图
        bool isVictory = SettlementTitleText.text.Contains("胜 利");
        if (isVictory)
        {
            MapManager.Instance.OnCombatVictory(currentNodeData);
        }
        else
        {
            Debug.LogError("【肉鸽终结】机甲全毁，本次探险结束！请大侠重新来过！");
        }
    }
}