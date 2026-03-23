using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 引入TMP用于文本显示

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    public bool IsCombatActive { get; private set; }

    [Header("=== UI 引用：战前部署 ===")]
    public GameObject CombatUIPanel;
    public Button StartBattleButton;

    // 👇【新增】：战后结算界面 UI 引用
    [Header("=== UI 引用：战后结算 ===")]
    public GameObject SettlementPanel;
    public TMP_Text SettlementTitleText;
    public Button ReturnToMapButton;

    [Header("=== 真实世界引用 ===")]
    public Transform EnemySpawnWorldPoint;
    public GameObject TestEnemyPrefab;

    private MapNodeData currentNodeData;

    // 👇【新增】：裁判的小本本，记录场上活着的单位
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

        // 绑定结算面板的返回按钮
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
        // 👇【防呆拦截】：开战前清点人数！没下兵不准开战！
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

        if (StartBattleButton != null) StartBattleButton.interactable = false;
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.CloseHangar();

        Debug.Log("【战斗导演】引擎轰鸣！发令枪响，全军出击！");

        IsCombatActive = true;
        isCheckingWinCondition = true; // 裁判开始吹哨！
    }

    // ==========================================
    // 阶段 2.5：裁判时刻 (每帧死盯血条)
    // ==========================================
    private void Update()
    {
        if (!IsCombatActive || !isCheckingWinCondition) return;

        // 1. 检查虫族是否死绝？
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

        // 2. 检查咱们的机甲是否全炸了？
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

        // 弹出结算面板
        if (SettlementPanel != null)
        {
            SettlementPanel.SetActive(true);
            if (SettlementTitleText != null)
            {
                SettlementTitleText.text = isVictory ? "战 斗 胜 利" : "任 务 失 败";
                SettlementTitleText.color = isVictory ? Color.green : Color.red;
            }
        }

        // TODO: 未来在这里可以生成战利品（随机抽取散件加入 PlayerInventoryManager）
    }

    // ==========================================
    // 阶段 4：打扫战场，班师回朝
    // ==========================================
    private void OnReturnToMapClicked()
    {
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        // 1. 打扫战场：强行销毁场上所有残留的物理肉体（尸体、残留子弹等）
        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            Destroy(r.gameObject);
        }

        Projectile[] allBullets = FindObjectsOfType<Projectile>();
        foreach (var b in allBullets) Destroy(b.gameObject);

        // 2. 数据归位：把出战的机甲状态重置为“在库闲置”，以便下个节点继续用！
        foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
        {
            if (profile != null) profile.IsDeployed = false;
        }

        // 3. 呼叫地图系统：我打完了，请切回地图！
        // 如果是胜利，告诉地图节点打勾；如果是失败，这里未来可以写 Roguelike 的 Game Over 逻辑。
        bool isVictory = SettlementTitleText.text.Contains("胜 利");
        if (isVictory)
        {
            MapManager.Instance.OnCombatVictory(currentNodeData);
        }
        else
        {
            Debug.LogError("【肉鸽终结】机甲全毁，本次探险结束！请大侠重新来过！");
            // TODO: 调用主菜单的重启大退逻辑
        }
    }
}