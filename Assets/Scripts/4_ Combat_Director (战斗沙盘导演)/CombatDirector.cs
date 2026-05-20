using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    [Header("=== 运行时单位注册表 ===")]
    public static List<DamageReceiver> ActiveEnemies = new List<DamageReceiver>();
    public static List<DamageReceiver> ActivePlayerUnits = new List<DamageReceiver>();

    public bool IsCombatActive { get; private set; }
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
    public GameObject ArenaReference;
    public GameObject BaseEnemyPrefab;
    public Sprite WhitePixelSprite;
    public GameObject ModularEnemyPrefab;

    public Vector3 CurrentArenaCenter { get; private set; }
    public Vector2 CurrentArenaSize { get; private set; }

    [Header("=== 战场边界防护 (空气墙) ===")]
    public float BoundaryThickness = 2f;
    private GameObject boundariesContainer;
    private GameObject activeEnemiesContainer;

    [Header("=== 运行时状态监视 (只读) ===")]
    public EncounterLayoutSO CurrentLayout;
    private GameObject forbiddenZonesContainer;

    [Header("=== 导航面板引用 ===")]
    public GameObject NavigationPanel;

    private MapNodeData currentNodeData;
    private bool isCheckingWinCondition = false;
    private bool isLastCombatVictory = false;
    private EventNodeSO pendingEventOnVictory;
    private EventNodeSO pendingEventOnFailure;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        IsCombatActive = true;
    }

    public static void ClearUnitRegistry()
    {
        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();
    }

    private void Start()
    {
        if (StartBattleButton != null) StartBattleButton.onClick.AddListener(OnBattleStartClicked);
        if (ReturnToMapButton != null) ReturnToMapButton.onClick.AddListener(OnReturnToMapClicked);
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (ArenaReference != null) ArenaReference.SetActive(false);
    }

    public void RegisterPostCombatEvents(EventNodeSO winEvent, EventNodeSO failEvent)
    {
        pendingEventOnVictory = winEvent;
        pendingEventOnFailure = failEvent;
    }

    public void EnterCombatPhase(MapNodeData nodeData, EncounterLayoutSO overrideLayout = null)
    {
        PerformFullCleanup();

        if (CombatUIPanel != null) CombatUIPanel.SetActive(true);
        if (StartBattleButton != null)
        {
            StartBattleButton.gameObject.SetActive(true);
            StartBattleButton.interactable = true;
            StartBattleButton.onClick.RemoveAllListeners();
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);
        }

        MusicManager.Instance?.SwitchState(MusicState.Combat);
        IsDeploymentPhase = true;
        IsCombatActive = false;
        isCheckingWinCondition = false;
        if (nodeData != null) currentNodeData = nodeData;

        if (overrideLayout != null)
        {
            this.CurrentLayout = overrideLayout;
        }
        else
        {
            pendingEventOnVictory = null;
            pendingEventOnFailure = null;

            if (RunManager.Instance != null)
            {
                int stage = RunManager.Instance.CurrentStage;
                int layer = (nodeData != null) ? nodeData.LayerIndex : 1;
                MapNodeType combatType = (nodeData != null) ? nodeData.HiddenRealType : MapNodeType.Enemy_Tech;
                this.CurrentLayout = RunManager.Instance.GetNextEncounter(stage, layer, combatType);
            }
        }

        if (this.CurrentLayout != null)
        {
            StartCoroutine(SafeInitArenaRoutine());
        }
        else
        {
            Debug.LogError("【加载失败】无法确定当前战场的布局配置！");
        }

        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.OpenHangar();
    }

    private System.Collections.IEnumerator SafeInitArenaRoutine()
    {
        yield return new WaitForEndOfFrame();

        SetupArenaVisuals();
        GenerateArenaBoundaries();
        SpawnEnemiesFromLayout();
        GenerateForbiddenZones();
    }

    private void SetupArenaVisuals()
    {
        if (ArenaReference == null) return;
        ArenaReference.SetActive(true);

        if (CurrentLayout != null && CurrentLayout.ArenaReference != null)
        {
            SpriteRenderer targetSR = CurrentLayout.ArenaReference.GetComponent<SpriteRenderer>();
            SpriteRenderer mySR = ArenaReference.GetComponent<SpriteRenderer>();
            ArenaBackgroundConfig bgConfig = CurrentLayout.ArenaReference.GetComponent<ArenaBackgroundConfig>();

            if (mySR != null)
            {
                if (bgConfig != null && bgConfig.RandomBackgrounds.Count > 0)
                {
                    Sprite chosenSprite = bgConfig.RandomBackgrounds[Random.Range(0, bgConfig.RandomBackgrounds.Count)];
                    mySR.sprite = chosenSprite;
                    if (targetSR != null) mySR.color = targetSR.color;
                }
                else if (targetSR != null && targetSR.sprite != null)
                {
                    mySR.sprite = targetSR.sprite;
                    mySR.color = targetSR.color;
                }
            }
        }
    }

    private void SpawnEnemiesFromLayout()
    {
        if (activeEnemiesContainer != null) Destroy(activeEnemiesContainer);
        activeEnemiesContainer = new GameObject("[Active Enemies]");
        activeEnemiesContainer.transform.SetParent(this.transform);

        if (CurrentLayout == null || CurrentLayout.Enemies == null) return;

        foreach (var spawnData in CurrentLayout.Enemies)
        {
            if (spawnData.EnemyType == null) continue;

            Vector3 spawnPos = CurrentArenaCenter + (Vector3)spawnData.LocalPosition;
            GameObject enemyObj;

            if (spawnData.EnemyType.Archetype == EnemyArchetype.Modular)
            {
                enemyObj = Instantiate(ModularEnemyPrefab, spawnPos, Quaternion.identity, activeEnemiesContainer.transform);
                MechUnit2D mechScript = enemyObj.GetComponent<MechUnit2D>();
                if (mechScript != null) mechScript.InitAsEliteEnemy(spawnData.EnemyType);
            }
            else
            {
                enemyObj = Instantiate(BaseEnemyPrefab, spawnPos, Quaternion.identity, activeEnemiesContainer.transform);
                EnemyBrain brain = enemyObj.GetComponent<EnemyBrain>();
                if (brain != null) brain.MyData = spawnData.EnemyType;
            }

            enemyObj.name = $"[Elite] {spawnData.EnemyType.EnemyName}";
        }
    }

    private void GenerateForbiddenZones()
    {
        if (forbiddenZonesContainer != null) Destroy(forbiddenZonesContainer);
        forbiddenZonesContainer = new GameObject("[Forbidden Zones]");
        forbiddenZonesContainer.transform.SetParent(this.transform);

        if (ArenaReference != null) forbiddenZonesContainer.transform.position = ArenaReference.transform.position;
        else forbiddenZonesContainer.transform.localPosition = Vector3.zero;

        forbiddenZonesContainer.transform.localScale = Vector3.one;

        if (CurrentLayout == null || CurrentLayout.ForbiddenZones == null) return;

        foreach (var rect in CurrentLayout.ForbiddenZones)
        {
            GameObject zonePhysicsObj = new GameObject("Zone_Blocker_Physics");
            zonePhysicsObj.transform.SetParent(forbiddenZonesContainer.transform);

            Vector3 zoneCenterPos = new Vector3(rect.x + rect.width / 2f, rect.y - rect.height / 2f, 0f);
            zonePhysicsObj.transform.localPosition = zoneCenterPos;

            BoxCollider2D col = zonePhysicsObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(rect.width, rect.height);
            col.isTrigger = true;
            zonePhysicsObj.layer = LayerMask.NameToLayer("NoDeploy");

            if (WhitePixelSprite != null)
            {
                GameObject visualObj = new GameObject("Zone_Visual");
                visualObj.transform.SetParent(zonePhysicsObj.transform);
                visualObj.transform.localPosition = Vector3.zero;
                SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
                sr.sprite = WhitePixelSprite;

                Vector2 spriteWorldSize = sr.sprite.bounds.size;
                visualObj.transform.localScale = new Vector3(rect.width / spriteWorldSize.x, rect.height / spriteWorldSize.y, 1f);

                sr.color = new Color(1f, 0f, 0f, 0.3f);
                sr.sortingLayerName = "DeployZone";
            }
        }
    }

    private void OnBattleStartClicked()
    {
        if (GameTerminalLog.Instance != null) GameTerminalLog.Instance.SetFreeze(true);

        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            if (r.isEnemy) ActiveEnemies.Add(r);
            else ActivePlayerUnits.Add(r);
        }

        if (ActivePlayerUnits.Count == 0) return;

        var allModularUnits = ActivePlayerUnits.Concat(ActiveEnemies).ToList();
        foreach (var unit in allModularUnits)
        {
            MechUnit2D mechScript = unit.GetComponent<MechUnit2D>();
            if (mechScript != null) mechScript.ExecuteBattleStartProtocol();
        }

        // 👇 精简重构：现在无脑调用即可，总署如果丢了它会自动重生
        if (CurrentLayout != null && CurrentLayout.ReinforcementData != null)
        {
            ReinforcementManager.Instance.gameObject.SetActive(true);
            ReinforcementManager.Instance.StartTimeline(CurrentLayout.ReinforcementData);
            Debug.Log("<color=green>[Director-Debug] 增援总署已连线，序列投递开始。</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>[Director-Debug] 本关卡未配置增援数据。</color>");
            ReinforcementManager.Instance.StopTimeline();
        }

        if (StartBattleButton != null) StartBattleButton.interactable = false;
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.CloseHangar();
        if (GlobalWarehouseUI.Instance != null) GlobalWarehouseUI.Instance.CloseWarehouse();
        if (NavHangarButton != null) NavHangarButton.interactable = false;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = false;
        if (forbiddenZonesContainer != null) forbiddenZonesContainer.SetActive(false);

        IsCombatActive = true;
        IsDeploymentPhase = false;
        isCheckingWinCondition = true;

        if (ActiveSkillUIManager.Instance != null) ActiveSkillUIManager.Instance.BuildSkillUI(ActivePlayerUnits);
        if (GlobalProtocolRegistry.Instance != null) GlobalProtocolRegistry.Instance.ApplyProtocolsToUnits(ActivePlayerUnits);
    }

    private void Update()
    {
        if (!IsCombatActive || !isCheckingWinCondition) return;

        bool isFieldEmpty = ActiveEnemies.All(e => e == null || e.CurrentHP <= 0);

        if (CurrentLayout != null && CurrentLayout.ReinforcementData != null)
        {
            bool isTimelineDone = (ReinforcementManager.Instance != null) && ReinforcementManager.Instance.IsTimelineFinished;
            if (isTimelineDone && isFieldEmpty)
            {
                TriggerSettlement(true);
                return;
            }
        }
        else
        {
            if (isFieldEmpty)
            {
                TriggerSettlement(true);
                return;
            }
        }

        bool allPlayersDead = ActivePlayerUnits.All(p => p == null || p.CurrentHP <= 0);
        if (allPlayersDead)
        {
            TriggerSettlement(false);
        }
    }

    private void TriggerSettlement(bool isVictory)
    {
        IsCombatActive = false;
        isCheckingWinCondition = false;
        isLastCombatVictory = isVictory;

        if (!isVictory)
        {
            int totalSanLoss = 0;
            foreach (var enemy in ActiveEnemies)
            {
                if (enemy != null && enemy.CurrentHP > 0)
                {
                    float hpPercent = enemy.CurrentHP / enemy.MaxHP;
                    EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                    if (brain != null && brain.MyData != null && brain.MyData.SanPenalties != null)
                    {
                        var sortedTiers = brain.MyData.SanPenalties.OrderByDescending(t => t.HpThreshold).ToList();
                        int currentLoss = 0;
                        foreach (var tier in sortedTiers)
                        {
                            if (hpPercent >= tier.HpThreshold) { currentLoss = tier.SanDeduction; break; }
                        }
                        totalSanLoss += currentLoss;
                    }
                }
            }

            if (GlobalResourceManager.Instance != null) GlobalResourceManager.Instance.ModifySAN(-totalSanLoss);
        }

        if (GlobalResourceManager.Instance != null && GlobalResourceManager.Instance.CurrentSAN <= 0)
        {
            if (SettlementPanel != null) SettlementPanel.SetActive(false);
            return;
        }

        if (SettlementPanel != null)
        {
            SettlementPanel.SetActive(true);
            if (SettlementTitleText != null)
            {
                SettlementTitleText.text = isVictory ? "战 斗 胜 利" : "任 务 失 败";
                SettlementTitleText.color = isVictory ? Color.green : Color.red;
            }
        }

        if (isVictory)
        {
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Combat_Victory);
            if (GlobalProtocolRegistry.Instance != null) GlobalProtocolRegistry.Instance.TickProtocolDurations();
        }
        else
        {
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Combat_Failure);
        }

        if (GameTerminalLog.Instance != null) GameTerminalLog.Instance.SetFreeze(false);
    }

    private void OnReturnToMapClicked()
    {
        LootSequenceSO encounterLoot = null;
        if (CurrentLayout != null) encounterLoot = CurrentLayout.NodeLootSequence;

        bool won = isLastCombatVictory;
        PerformFullCleanup();

        if (won && encounterLoot != null && LootSequenceDirector.Instance != null)
        {
            LootSequenceDirector.Instance.StartLootHub(encounterLoot, null, MacroCategory.Tech, 1, () => ExecuteReturnToMap());
        }
        else
        {
            ExecuteReturnToMap();
        }
    }

    public void ExecuteReturnToMap()
    {
        EventNodeSO nextStep = isLastCombatVictory ? pendingEventOnVictory : pendingEventOnFailure;
        pendingEventOnVictory = null;
        pendingEventOnFailure = null;

        MusicManager.Instance?.SwitchState(MusicState.Map);
        PerformFullCleanup();

        if (nextStep != null && EventDirector.Instance != null)
        {
            if (MapManager.Instance != null && MapManager.Instance.MapUIPanel != null)
                MapManager.Instance.MapUIPanel.SetActive(false);
            EventDirector.Instance.PlayEvent(nextStep);
        }
        else
        {
            if (MapManager.Instance != null && currentNodeData != null)
            {
                MapManager.Instance.OnCombatVictory(currentNodeData);
                currentNodeData = null;
            }
            else if (MapManager.Instance != null)
            {
                MapManager.Instance.MapUIPanel.SetActive(true);
            }
        }
    }

    public void SetNavigationVisibility(bool isVisible)
    {
        if (NavigationPanel != null) NavigationPanel.SetActive(isVisible);
    }

    public void PerformFullCleanup()
    {
        ResetBattlefieldInternal();
    }

    private void ResetBattlefieldInternal()
    {
        if (PlayerInventoryManager.Instance != null && PlayerInventoryManager.Instance.HangarUnits != null)
        {
            foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
            {
                if (profile != null) profile.IsDeployed = false;
            }
        }

        if (ActiveSkillUIManager.Instance != null)
        {
            ActiveSkillUIManager.Instance.ClearUI();
            ActiveSkillUIManager.Instance.SetVisibility(false);
        }

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        SimplePool.ClearPool();
        IsCombatActive = false;
        IsDeploymentPhase = false;
        CurrentLayout = null;

        if (activeEnemiesContainer != null)
        {
            foreach (Transform child in activeEnemiesContainer.transform) Destroy(child.gameObject);
        }

        MechUnit2D[] allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var mech in allMechs)
        {
            if (mech != null)
            {
                mech.SyncPostCombatState();
                Destroy(mech.gameObject);
            }
        }

        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        if (ArenaReference != null) ArenaReference.SetActive(false);
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        StopAllCoroutines();
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.RefreshHangar();
    }

    private void GenerateArenaBoundaries()
    {
        CurrentArenaCenter = ArenaReference != null ? ArenaReference.transform.position : Vector3.zero;
        CurrentArenaSize = new Vector2(24f, 14f);

        if (ArenaReference != null)
        {
            BoxCollider2D arenaCol = ArenaReference.GetComponent<BoxCollider2D>();
            if (arenaCol != null)
            {
                CurrentArenaCenter = ArenaReference.transform.TransformPoint(arenaCol.offset);
                CurrentArenaSize = new Vector2(
                    arenaCol.size.x * ArenaReference.transform.lossyScale.x,
                    arenaCol.size.y * ArenaReference.transform.lossyScale.y
                );
            }
        }

        float thickness = 20f;
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
    }

    private MacroCategory GetMacroForNodeType(MapNodeType type) { return MacroCategory.Tech; }

    public void FullResetBeforeExit()
    {
        Time.timeScale = 1f;
        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        if (activeEnemiesContainer != null) Destroy(activeEnemiesContainer);
        if (boundariesContainer != null) Destroy(boundariesContainer);
        if (forbiddenZonesContainer != null) Destroy(forbiddenZonesContainer);

        var allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var m in allMechs) Destroy(m.gameObject);
        var allProjectiles = FindObjectsOfType<Projectile>();
        foreach (var p in allProjectiles) Destroy(p.gameObject);

        IsCombatActive = false;
        IsDeploymentPhase = false;
        CurrentLayout = null;
        currentNodeData = null;

        if (ArenaReference != null) ArenaReference.SetActive(false);
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetImmersionMode(false);
            MusicManager.Instance.SwitchState(MusicState.Silence);
        }
    }
}