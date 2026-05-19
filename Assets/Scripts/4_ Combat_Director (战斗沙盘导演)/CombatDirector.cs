using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    // ==========================================
    // 🚀 全局单位注册中心 (O(1) 检索优化)
    // ==========================================
    [Header("=== 运行时单位注册表 ===")]
    public static List<DamageReceiver> ActiveEnemies = new List<DamageReceiver>();
    public static List<DamageReceiver> ActivePlayerUnits = new List<DamageReceiver>();

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
    public GameObject ArenaReference;
    public GameObject BaseEnemyPrefab;
    public Sprite WhitePixelSprite;
    public GameObject ModularEnemyPrefab;  // 【新增】精英组装机预制体

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
    public GameObject NavigationPanel; // 👈 拖入包含“进入仓库、进入机库”按钮的父物体

    private MapNodeData currentNodeData;
    private bool isCheckingWinCondition = false;
    private bool isLastCombatVictory = false;
    private EventNodeSO pendingEventOnVictory;
    private EventNodeSO pendingEventOnFailure;



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    /// <summary>
    /// 快速清理全场注册表
    /// </summary>
    public static void ClearUnitRegistry()
    {
        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();
        Debug.Log("<color=white>【系统】</color> 战场单位注册表已清空。");
    }

    private void Start()
    {
        if (StartBattleButton != null)
        {
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);
        }

        if (ReturnToMapButton != null)
        {
            ReturnToMapButton.onClick.AddListener(OnReturnToMapClicked);
        }

        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (ArenaReference != null) ArenaReference.SetActive(false);
    }

    /// <summary>
    /// 关卡入口：进入部署阶段
    /// </summary>
    public void RegisterPostCombatEvents(EventNodeSO winEvent, EventNodeSO failEvent)
    {
        pendingEventOnVictory = winEvent;
        pendingEventOnFailure = failEvent;
        Debug.Log("<color=#00FFFF>【剧情挂载】</color> 成功注册后续事件钩子。");
    }
    public void EnterCombatPhase(MapNodeData nodeData, EncounterLayoutSO overrideLayout = null)
    {
        // A. 执行物理层面的清理
        PerformFullCleanup();

        // B. UI 强制唤醒
        if (CombatUIPanel != null) CombatUIPanel.SetActive(true);
        if (StartBattleButton != null)
        {
            StartBattleButton.gameObject.SetActive(true);
            StartBattleButton.interactable = true;
            StartBattleButton.onClick.RemoveAllListeners();
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);
        }

        // C. 状态初始化
        MusicManager.Instance?.SwitchState(MusicState.Combat);
        IsDeploymentPhase = true;
        IsCombatActive = false;
        isCheckingWinCondition = false;
        if (nodeData != null) currentNodeData = nodeData;

        // D. 【关键：确定布局数据】
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

        // --- 👇【核心加固逻辑】：只有布局确定后，再处理增援 ---
        if (this.CurrentLayout != null)
        {
            // 1. 物理环境实例化
            SetupArenaVisuals();
            SpawnEnemiesFromLayout();
            GenerateForbiddenZones();

            // 2. 启动增援时间轴 (增加 Instance 判空保护)
          
        }
        else
        {
            Debug.LogError("【加载失败】无法确定当前战场的布局配置！");
        }

        // F. 打开机库准备部署
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.OpenHangar();
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

        Vector3 centerPos = CurrentArenaCenter;
        if (CurrentLayout == null || CurrentLayout.Enemies == null) return;

        foreach (var spawnData in CurrentLayout.Enemies)
        {
            if (spawnData.EnemyType == null) continue;

            Vector3 spawnPos = CurrentArenaCenter + (Vector3)spawnData.LocalPosition;
            GameObject enemyObj;

            // 【核心流控】：判断敌人类型
            if (spawnData.EnemyType.Archetype == EnemyArchetype.Modular)
            {
                // A. 生成组装精英
                enemyObj = Instantiate(ModularEnemyPrefab, spawnPos, Quaternion.identity, activeEnemiesContainer.transform);

                MechUnit2D mechScript = enemyObj.GetComponent<MechUnit2D>();
                if (mechScript != null)
                {
                    // 调用我们写好的 InitAsEliteEnemy，它会处理所有层级、AI和零件
                    mechScript.InitAsEliteEnemy(spawnData.EnemyType);
                }
            }
            else
            {
                // B. 生成静态杂兵 (保持原逻辑)
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

        // --- 👇【关键修正 A】：对齐物理参考点 ---
        // 强制禁区容器的坐标与战场地板保持绝对一致，保证坐标系的原点重合
        if (ArenaReference != null)
            forbiddenZonesContainer.transform.position = ArenaReference.transform.position;
        else
            forbiddenZonesContainer.transform.localPosition = Vector3.zero;

        forbiddenZonesContainer.transform.localScale = Vector3.one;

        if (CurrentLayout == null || CurrentLayout.ForbiddenZones == null) return;

        foreach (var rect in CurrentLayout.ForbiddenZones)
        {
            GameObject zonePhysicsObj = new GameObject("Zone_Blocker_Physics");
            zonePhysicsObj.transform.SetParent(forbiddenZonesContainer.transform);

            // --- 👇【关键修正 B】：左上角转中心点算法 ---
            // rect.x, rect.y 是你在编辑器里画出的“矩形左上角”相对于中心点的坐标
            // 中心点 X = 左边距 + 宽度的一半
            // 中心点 Y = 顶边距 - 高度的一半 (Unity世界坐标向下为负)
            Vector3 zoneCenterPos = new Vector3(rect.x + rect.width / 2f, rect.y - rect.height / 2f, 0f);
            zonePhysicsObj.transform.localPosition = zoneCenterPos;

            // 注入物理碰撞
            BoxCollider2D col = zonePhysicsObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(rect.width, rect.height);
            col.isTrigger = true;
            zonePhysicsObj.layer = LayerMask.NameToLayer("NoDeploy");

            // 注入红色半透明视觉
            if (WhitePixelSprite != null)
            {
                GameObject visualObj = new GameObject("Zone_Visual");
                visualObj.transform.SetParent(zonePhysicsObj.transform);
                visualObj.transform.localPosition = Vector3.zero;
                SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
                sr.sprite = WhitePixelSprite;

                // 归一化缩放：目标宽高 / 图片原始尺寸
                Vector2 spriteWorldSize = sr.sprite.bounds.size;
                visualObj.transform.localScale = new Vector3(rect.width / spriteWorldSize.x, rect.height / spriteWorldSize.y, 1f);

                sr.color = new Color(1f, 0f, 0f, 0.3f);
                sr.sortingLayerName = "DeployZone";
            }
        }
    }
    /// <summary>
    /// 【核心事件】：玩家点击“开始战斗”
    /// </summary>
    /// <summary>
    /// 【核心事件】：玩家点击“开始战斗”
    /// </summary>
    private void OnBattleStartClicked()
    {
        if (GameTerminalLog.Instance != null) GameTerminalLog.Instance.SetFreeze(true);
        // 1. 重新抓取当前部署在场上的所有单位
        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        foreach (var r in allReceivers)
        {
            if (r.isEnemy) ActiveEnemies.Add(r);
            else ActivePlayerUnits.Add(r);
        }

        // 判定：如果场上一台机甲都没有，不允许开战
        if (ActivePlayerUnits.Count == 0)
        {
            Debug.LogWarning("【战斗导演】警告：未部署任何单位，无法开战！");
            return;
        }

      

        // 2. 通知全战场所有“组装型”单位执行开战协议 (包括玩家和精英敌人)
        var allModularUnits = ActivePlayerUnits.Concat(ActiveEnemies).ToList();

        foreach (var unit in allModularUnits)
        {
            MechUnit2D mechScript = unit.GetComponent<MechUnit2D>();
            if (mechScript != null)
            {
                mechScript.ExecuteBattleStartProtocol();
            }
        }

        if (CurrentLayout != null && CurrentLayout.ReinforcementData != null)
        {
            if (ReinforcementManager.Instance != null)
            {
                ReinforcementManager.Instance.StartTimeline(CurrentLayout.ReinforcementData);
            }
        }
        else if (ReinforcementManager.Instance != null)
        {
            ReinforcementManager.Instance.StopTimeline();
        }


        // 3. UI 状态锁定：防止战斗中进行非法操作
        if (StartBattleButton != null) StartBattleButton.interactable = false;

        // 强制关闭已打开的整备界面
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.CloseHangar();
        if (GlobalWarehouseUI.Instance != null) GlobalWarehouseUI.Instance.CloseWarehouse();

        // 锁死主导航栏
        if (NavHangarButton != null) NavHangarButton.interactable = false;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = false;

        // 隐藏部署禁区提示
        if (forbiddenZonesContainer != null) forbiddenZonesContainer.SetActive(false);

        // 4. 正式鸣枪：变更导演状态
        IsCombatActive = true;
        IsDeploymentPhase = false;
        isCheckingWinCondition = true;

        // 5. 构建并显示主动技能栏
        if (ActiveSkillUIManager.Instance != null)
        {
            ActiveSkillUIManager.Instance.BuildSkillUI(ActivePlayerUnits);
        }

        // 6. 应用全局协议 (Protocols)
        if (GlobalProtocolRegistry.Instance != null)
        {
            GlobalProtocolRegistry.Instance.ApplyProtocolsToUnits(ActivePlayerUnits);
        }

        Debug.Log("<color=green>【战斗导演】全员通电完成，逻辑闭环已启动，战斗正式开始！</color>");
    }
    // --- 替换 CombatDirector.cs 中的整个 Update 方法 ---
    // --- CombatDirector.cs 修改后的 Update 方法 ---
    private void Update()
    {
        // 1. 处理空气墙生成
        if (!wallsGenerated)
        {
            if (ArenaReference == null) return;
            GenerateArenaBoundaries();
            wallsGenerated = true;
        }

        // 2. 状态锁：只有在战斗激活且正在检查胜负时才继续
        if (!IsCombatActive || !isCheckingWinCondition) return;

        // ---------------------------------------------------------
        // ⚔️ 核心胜利判定分支
        // ---------------------------------------------------------

        // 检查场上当前是否有活着的敌人
        bool isFieldEmpty = ActiveEnemies.All(e => e == null || e.CurrentHP <= 0);

        // 分支 A：当前布局配置了【增援数据】
        if (CurrentLayout != null && CurrentLayout.ReinforcementData != null)
        {
            // 调用我们补全的 IsTimelineFinished
            bool isTimelineDone = (ReinforcementManager.Instance != null) && ReinforcementManager.Instance.IsTimelineFinished;

            // 增援战胜利条件：所有阶段投递完毕 且 场上敌人杀光
            if (isTimelineDone && isFieldEmpty)
            {
                TriggerSettlement(true);
                return;
            }
        }
        // 分支 B：普通布局（没有增援）
        else
        {
            // 歼灭战胜利条件：只要场上杀光即赢
            if (isFieldEmpty)
            {
                TriggerSettlement(true);
                return;
            }
        }

        // ---------------------------------------------------------
        // 💀 核心失败判定
        // ---------------------------------------------------------

        // 检查玩家单位存活状态
        bool allPlayersDead = ActivePlayerUnits.All(p => p == null || p.CurrentHP <= 0);

        if (allPlayersDead)
        {
            TriggerSettlement(false);
        }
    }

    private void TriggerSettlement(bool isVictory)
    {
        // 1. 立即停止战斗心跳，但先不要弹窗
        IsCombatActive = false;
        isCheckingWinCondition = false;
        isLastCombatVictory = isVictory;

        // 2. 如果战斗失败，计算 SAN 值扣除
        if (!isVictory)
        {
            int totalSanLoss = 0;
            foreach (var enemy in ActiveEnemies)
            {
                if (enemy != null && enemy.CurrentHP > 0)
                {
                    // ... 保持原有的根据 HP 阈值计算 SAN 扣除的逻辑 ...
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

            // 核心步骤：扣除 SAN 值
            if (GlobalResourceManager.Instance != null)
            {
                GlobalResourceManager.Instance.ModifySAN(-totalSanLoss);
            }
        }

        // 3. 👇【核心修复：互斥检查】
        // 如果扣完 SAN 值后，发现指挥官已经疯了（<= 0），则直接退出，绝不显示结算面板
        if (GlobalResourceManager.Instance != null && GlobalResourceManager.Instance.CurrentSAN <= 0)
        {
            Debug.Log("<color=red>【系统】</color> 检测到指挥官精神崩溃，结算面板已熔断。");
            if (SettlementPanel != null) SettlementPanel.SetActive(false); // 强制关闭可能残留的面板
            return;
        }

        // 4. 只有在指挥官还清醒的情况下，才显示正常的结算 UI
        if (SettlementPanel != null)
        {
            SettlementPanel.SetActive(true);
            if (SettlementTitleText != null)
            {
                SettlementTitleText.text = isVictory ? "战 斗 胜 利" : "任 务 失 败";
                SettlementTitleText.color = isVictory ? Color.green : Color.red;
            }
        }

        // 5. 播放对应的音效
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
        // --- 👇【核心修正 A】：在清理之前，先把奖励引用存下来 ---
        LootSequenceSO encounterLoot = null;
        if (CurrentLayout != null)
        {
            encounterLoot = CurrentLayout.NodeLootSequence;
        }

        // 记录当前的获胜状态
        bool won = isLastCombatVictory;
        // ----------------------------------------------------

        // --- 👇【核心修正 B】：执行物理清理 ---
        // 此时清理掉 CurrentLayout 已经没关系了，因为我们已经拿到了 encounterLoot
        PerformFullCleanup();

        // --- 👇【核心修正 C】：基于缓存的数据决定后续去向 ---
        if (won && encounterLoot != null && LootSequenceDirector.Instance != null)
        {
            Debug.Log("<color=green>【系统】</color> 侦测到战利品序列，正在打开大巴扎...");

            // 注意：这里的回调函数必须指向 ExecuteReturnToMap，确保选完后能正常回图
            LootSequenceDirector.Instance.StartLootHub(encounterLoot, null, MacroCategory.Tech, 1, () => ExecuteReturnToMap());
        }
        else
        {
            // 如果没赢，或者没配奖励，直接回地图
            ExecuteReturnToMap();
        }
    }
    public void ExecuteReturnToMap()
    {
        // --- 1. 先把钩子拿出来缓存，然后立刻切断寄存器 ---
        EventNodeSO nextStep = isLastCombatVictory ? pendingEventOnVictory : pendingEventOnFailure;
        pendingEventOnVictory = null;
        pendingEventOnFailure = null;

        // --- 2. 正常执行音频和物理清理 ---
        MusicManager.Instance?.SwitchState(MusicState.Map);
        PerformFullCleanup();

        // --- 3. 跳转判定 ---
        if (nextStep != null && EventDirector.Instance != null)
        {
            Debug.Log($"<color=#00FFFF>【逻辑拦截】</color> 检测到后续剧情，正在执行跳转：{nextStep.EventTitle}");

            // 确保地图面板不干扰，直接唤醒事件导演
            if (MapManager.Instance != null && MapManager.Instance.MapUIPanel != null)
                MapManager.Instance.MapUIPanel.SetActive(false);

            EventDirector.Instance.PlayEvent(nextStep);
        }
        else
        {
            // 正常的普通回城流程
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
        if (NavigationPanel != null)
        {
            NavigationPanel.SetActive(isVisible);
            Debug.Log($"<color=white>【导航总线】</color> 底部导航栏已 {(isVisible ? "唤醒" : "沉默")}");
        }
    }

    public void PerformFullCleanup()
    {
        Debug.Log("<color=#FF00FF>【战区管理】当前作战环境已物理卸载，逻辑回滚至待命态。</color>");
        ResetBattlefieldInternal();
    }

    // 核心重置逻辑提取
    private void ResetBattlefieldInternal()
    {
        // --- 1. 逻辑状态返还 ---
        if (PlayerInventoryManager.Instance != null && PlayerInventoryManager.Instance.HangarUnits != null)
        {
            foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
            {
                if (profile != null) profile.IsDeployed = false;
            }
        }

        // --- 2. UI 视觉清理 ---
        if (ActiveSkillUIManager.Instance != null)
        {
            ActiveSkillUIManager.Instance.ClearUI();
            ActiveSkillUIManager.Instance.SetVisibility(false);
        }

        if (NavHangarButton != null) NavHangarButton.interactable = true;
        if (NavWarehouseButton != null) NavWarehouseButton.interactable = true;

        // --- 3. 对象池与状态机重置 ---
        SimplePool.ClearPool(); // 彻底肃清旧子弹/飘字引用

        IsCombatActive = false;
        IsDeploymentPhase = false;
        wallsGenerated = false;
        CurrentLayout = null;

        // --- 4. 物理实体物理卸载 ---
        if (activeEnemiesContainer != null)
        {
            foreach (Transform child in activeEnemiesContainer.transform) Destroy(child.gameObject);
        }

        MechUnit2D[] allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var mech in allMechs)
        {
            if (mech != null)
            {
                mech.SyncPostCombatState(); // 执行满血复活同步
                Destroy(mech.gameObject);
            }
        }

        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        // --- 5. 场景预制体归位 ---
        if (ArenaReference != null) ArenaReference.SetActive(false);
        if (SettlementPanel != null) SettlementPanel.SetActive(false);
        if (CombatUIPanel != null) CombatUIPanel.SetActive(false);

        StopAllCoroutines();

        // 刷新机库 UI
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
        Debug.Log("<color=red>【指令】执行紧急关停协议，物理卸载全场实体...</color>");

        // 1. 强制重置时间缩放（防止在暂停状态退出导致新游戏卡死）
        Time.timeScale = 1f;

        // 2. 彻底清空静态注册表 (防止残留野指针)
        ActiveEnemies.Clear();
        ActivePlayerUnits.Clear();

        // 3. 物理清理：销毁所有战场动态生成的容器
        if (activeEnemiesContainer != null) Destroy(activeEnemiesContainer);
        if (boundariesContainer != null) Destroy(boundariesContainer);
        if (forbiddenZonesContainer != null) Destroy(forbiddenZonesContainer);

        // 4. 寻找并清理所有残余的战斗单位和子弹（双重保险）
        var allMechs = FindObjectsOfType<MechUnit2D>();
        foreach (var m in allMechs) Destroy(m.gameObject);

        var allProjectiles = FindObjectsOfType<Projectile>();
        foreach (var p in allProjectiles) Destroy(p.gameObject);

        // 5. 状态机归零
        IsCombatActive = false;
        IsDeploymentPhase = false;
        wallsGenerated = false;
        CurrentLayout = null;
        currentNodeData = null;

        // 6. 核心战场预制体复位
        if (ArenaReference != null)
        {
            ArenaReference.SetActive(false);
        }

        // 7. 音乐恢复
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetImmersionMode(false);
            MusicManager.Instance.SwitchState(MusicState.Silence);
        }
    }
}