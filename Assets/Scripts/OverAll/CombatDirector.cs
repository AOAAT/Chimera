using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    public bool IsCombatActive { get; private set; }
    public bool IsDeploymentPhase { get; private set; }

    private bool wallsGenerated = false;

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
    [Tooltip("拖入场景中的战斗场地预制体节点！系统会自动读取它的 BoxCollider2D 作为绝对物理边界")]
    public GameObject ArenaReference;

    [Tooltip("怪物的通用空壳预制体 (必须挂载 EnemyBrain)")]
    public GameObject BaseEnemyPrefab;

    [Tooltip("一张纯白色的 1x1 像素贴图，用于可视化红区")]
    public Sprite WhitePixelSprite;

    public Vector3 CurrentArenaCenter { get; private set; }
    public Vector2 CurrentArenaSize { get; private set; }
    [Header("=== 战场边界防护 (空气墙) ===")]
    public float BoundaryThickness = 2f;
    private GameObject boundariesContainer;
    private GameObject activeEnemiesContainer;

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

    public void EnterCombatPhase(MapNodeData nodeData)
    {
        IsCombatActive = false;
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

        // 👇【核心修复】：呼叫发牌官时，现在只需要传 3 个参数了！去掉了 Theme！
        int currentStage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
        int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;

        CurrentLayout = RunManager.Instance.GetNextEncounter(currentStage, currentLayer, nodeData.NodeType);

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
        if (activeEnemiesContainer != null) Destroy(activeEnemiesContainer);
        activeEnemiesContainer = new GameObject("[Active Enemies]");
        activeEnemiesContainer.transform.SetParent(this.transform);

        Vector3 centerPos = CurrentArenaCenter;

        // 【回归初心】：严格按照策划在 EncounterLayoutSO 里的配置刷怪！
        foreach (var spawnData in CurrentLayout.Enemies)
        {
            if (spawnData.EnemyType == null) continue;

            Vector3 spawnPos = centerPos + new Vector3(spawnData.LocalPosition.x, spawnData.LocalPosition.y, 0f);
            GameObject enemyObj = Instantiate(BaseEnemyPrefab, spawnPos, Quaternion.identity, activeEnemiesContainer.transform);
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

        Vector3 centerPos = CurrentArenaCenter;

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

    private void GenerateArenaBoundaries()
    {
        CurrentArenaCenter = ArenaReference.transform.position;
        CurrentArenaSize = new Vector2(24f, 14f); // 兜底大小

        BoxCollider2D arenaCol = ArenaReference.GetComponent<BoxCollider2D>();
        if (arenaCol != null)
        {
            CurrentArenaCenter = ArenaReference.transform.TransformPoint(arenaCol.offset);
            CurrentArenaSize = new Vector2(
                arenaCol.size.x * ArenaReference.transform.lossyScale.x,
                arenaCol.size.y * ArenaReference.transform.lossyScale.y
            );
        }

        float thickness = 20f; // 20米厚的防爆墙
        Vector2 size = CurrentArenaSize;
        Vector3 center = CurrentArenaCenter;

        if (boundariesContainer != null) Destroy(boundariesContainer);
        boundariesContainer = new GameObject("[Arena Boundaries]");
        boundariesContainer.transform.SetParent(this.transform);

        CreateWall("AirWall_Top", center + new Vector3(0, size.y / 2 + thickness / 2, 0), new Vector2(size.x + thickness * 2, thickness));
        CreateWall("AirWall_Bottom", center + new Vector3(0, -size.y / 2 - thickness / 2, 0), new Vector2(size.x + thickness * 2, thickness));
        CreateWall("AirWall_Left", center + new Vector3(-size.x / 2 - thickness / 2, 0, 0), new Vector2(thickness, size.y));
        CreateWall("AirWall_Right", center + new Vector3(size.x / 2 + thickness / 2, 0, 0), new Vector2(thickness, size.y));
    }

    private void CreateWall(string wallName, Vector3 pos, Vector2 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(boundariesContainer.transform);
        wall.transform.position = pos;
        wall.layer = LayerMask.NameToLayer("Default");

        wall.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = false;

        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        if (WhitePixelSprite != null)
        {
            sr.sprite = WhitePixelSprite;
            sr.color = new Color(1f, 0f, 0f, 0.15f);
        }
    }

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
        IsDeploymentPhase = false;
        isCheckingWinCondition = true;
    }

    private void Update()
    {
        if (!wallsGenerated)
        {
            if (ArenaReference == null) return;
            GenerateArenaBoundaries();
            wallsGenerated = true;
        }

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

    private void TriggerSettlement(bool isVictory)
    {
        IsCombatActive = false;
        isCheckingWinCondition = false;
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

        // 👇【核心新增】：如果是战败，结算全局扣血 (SAN)！
        if (!isVictory)
        {
            int totalSanLoss = 0;
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && enemy.CurrentHP > 0)
                {
                    float hpPercent = enemy.CurrentHP / enemy.MaxHP;
                    EnemyBrain brain = enemy.GetComponent<EnemyBrain>();

                    if (brain != null && brain.MyData != null && brain.MyData.SanPenalties != null)
                    {
                        // 将阶梯按血量从高到低排序，找到第一个符合区间的惩罚
                        var sortedTiers = brain.MyData.SanPenalties.OrderByDescending(t => t.HpThreshold).ToList();
                        int currentLoss = 0;

                        foreach (var tier in sortedTiers)
                        {
                            if (hpPercent >= tier.HpThreshold)
                            {
                                currentLoss = tier.SanDeduction;
                                break;
                            }
                        }
                        totalSanLoss += currentLoss;
                        Debug.Log($"【战败清算】[{brain.MyData.EnemyName}] 剩余血量 {hpPercent:P0}，导致玩家扣除 {currentLoss} 点 SAN。");
                    }
                }
            }

            Debug.Log($"<color=#FF0000>【系统结算】本次战役总计损失 {totalSanLoss} 点 SAN 值！</color>");
            if (GlobalResourceManager.Instance != null)
            {
                GlobalResourceManager.Instance.ModifySAN(-totalSanLoss);
            }
        }
    }

    private void OnReturnToMapClicked()
    {
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
        if (boundariesContainer != null) Destroy(boundariesContainer);

        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
        {
            if (profile != null) profile.IsDeployed = false;
        }

        bool isVictory = SettlementTitleText != null && SettlementTitleText.text.Contains("胜 利");
        if (isVictory)
        {
            // 来源 A：遭遇战的特定掉落
            LootSequenceSO encounterLoot = CurrentLayout != null ? CurrentLayout.NodeLootSequence : null;

            // 来源 B：👇向全新的全局节点大脑要掉落补偿！
            int currentStage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
            int currentLayer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;
            MapNodeType currentType = currentNodeData != null ? currentNodeData.NodeType : MapNodeType.Enemy_Tech;

            LootSequenceSO nodeLoot = NodeLootManager.Instance != null ? NodeLootManager.Instance.GetLootForNode(currentStage, currentLayer, currentType) : null;

            MacroCategory currentMacro = GetMacroForNodeType(currentType);

            LootSequenceDirector.Instance.StartLootHub(encounterLoot, nodeLoot, currentMacro, currentLayer);
        }
        else
        {
            Debug.LogError("【肉鸽终结】机甲全毁，本次探险结束！请重新来过！");
            // TODO: 未来接 Game Over 界面
        }
    }
    // 👇【核心修复】：精准地把新的节点类型，翻译成战利品大巴扎的大类！
    private MacroCategory GetMacroForNodeType(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Enemy_Flesh:
                return MacroCategory.Flesh;
            case MapNodeType.Enemy_Magic:
                return MacroCategory.Magic;
            case MapNodeType.Enemy_Tech:
            case MapNodeType.Enemy_Mixed:
            default:
                return MacroCategory.Tech; // 兜底给科技
        }
    }

    public void ExecuteReturnToMap()
    {
        Debug.Log("【战斗导演】系统交接完毕，将指挥权交还给大地图...");
        MapManager.Instance.OnCombatVictory(currentNodeData);
    }

    private void OnDrawGizmos()
    {
        if (ArenaReference != null)
        {
            Vector3 center = ArenaReference.transform.position;
            Vector2 size = new Vector2(24f, 14f);
            BoxCollider2D col = ArenaReference.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                center = ArenaReference.transform.TransformPoint(col.offset);
                size = new Vector2(col.size.x * ArenaReference.transform.lossyScale.x, col.size.y * ArenaReference.transform.lossyScale.y);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(center + Vector3.up * 2, center + Vector3.down * 2);
            Gizmos.DrawLine(center + Vector3.left * 2, center + Vector3.right * 2);
            Gizmos.DrawWireSphere(center, 0.5f);

            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0));
        }
    }
}