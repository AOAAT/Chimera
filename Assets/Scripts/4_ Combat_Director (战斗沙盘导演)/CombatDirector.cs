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

    private MapNodeData currentNodeData;
    private bool isCheckingWinCondition = false;
    private bool isLastCombatVictory = false;

    

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

    public void EnterCombatPhase(MapNodeData nodeData)
    {
        // --- 1. 系统防护：执行物理与逻辑大扫除 ---
        // 如果是从非正常状态（如战斗中强退重进）触发，执行带日志的重置
        if (IsCombatActive || IsDeploymentPhase)
        {
            Debug.Log("<color=yellow>【避灾协议】检测到异常残留环境，正在执行强制全量重置...</color>");
            PerformFullCleanup();
        }
        else
        {
            // 正常逻辑流（战斗已结算）下，执行静默重置，确保战场是一张白纸
            // 注意：ResetBattlefieldInternal 是我建议你封装的私有方法，
            // 如果你还没封装，这里可以直接先调 PerformFullCleanup()
            PerformFullCleanup();
        }

        // --- 2. 核心 UI 唤醒链路 (解决按钮不显示的问题) ---
        if (CombatUIPanel != null)
        {
            CombatUIPanel.SetActive(true);
        }

        if (StartBattleButton != null)
        {
            StartBattleButton.gameObject.SetActive(true);
            StartBattleButton.interactable = true;

            // 【补强】：工业级监听重刷。
            // 防止场景切换或退出导致的 Button 引用丢失或逻辑挂空
            StartBattleButton.onClick.RemoveAllListeners();
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);
        }

        // --- 3. 导演状态机重置 ---
        MusicManager.Instance?.SwitchState(MusicState.Combat);
        IsDeploymentPhase = true;  // 进入部署阶段
        IsCombatActive = false;    // 战斗尚未鸣枪
        isCheckingWinCondition = false;

        // --- 4. 节点数据交接 ---
        if (nodeData != null)
        {
            currentNodeData = nodeData;
            Debug.Log($"<color=green>【导演】</color> 已锁定作战节点: {currentNodeData.NodeID}");
        }

        // --- 5. 环境与布局加载 ---
        if (RunManager.Instance == null)
        {
            Debug.LogError("【严重错误】RunManager 实例丢失，无法生成关卡！");
            return;
        }

        int stage = RunManager.Instance.CurrentStage;
        int layer = MapManager.Instance != null ? MapManager.Instance.CurrentLayer : 1;
        MapNodeType combatType = (currentNodeData != null) ? currentNodeData.HiddenRealType : MapNodeType.Enemy_Mixed;

        // 从牌库抓取具体的战斗配置
        CurrentLayout = RunManager.Instance.GetNextEncounter(stage, layer, combatType);

        if (CurrentLayout != null)
        {
            SetupArenaVisuals();    // 这里面会执行 ArenaReference.SetActive(true)
            SpawnEnemiesFromLayout();
            GenerateForbiddenZones();

            Debug.Log($"<color=cyan>【系统】</color> 战场布局 [{CurrentLayout.name}] 已加载，等待部署。");
        }
        else
        {
            Debug.LogError($"【加载失败】找不到匹配 Stage:{stage} Layer:{layer} 的战斗配置！");
        }

        // --- 6. 自动唤起整备 UI ---
        if (HangarMenuUI.Instance != null)
        {
            HangarMenuUI.Instance.OpenHangar();
        }
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

        Vector3 centerPos = CurrentArenaCenter;
        int noDeployLayer = LayerMask.NameToLayer("NoDeploy");

        if (CurrentLayout == null || CurrentLayout.ForbiddenZones == null) return;

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
                sr.sortingLayerName = "DeployZone";
                sr.sortingOrder = 0;
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
    private void Update()
    {
        if (!wallsGenerated)
        {
            if (ArenaReference == null) return;
            GenerateArenaBoundaries();
            wallsGenerated = true;
        }

        if (!IsCombatActive || !isCheckingWinCondition) return;

        bool allEnemiesDead = ActiveEnemies.All(e => e == null || e.CurrentHP <= 0);
        if (allEnemiesDead)
        {
            TriggerSettlement(true);
            return;
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
        IsDeploymentPhase = false;
        isLastCombatVictory = isVictory;

        if (SettlementPanel != null)
        {
            SettlementPanel.SetActive(true);
            if (SettlementTitleText != null)
            {
                SettlementTitleText.text = isVictory ? "战 斗 胜 利" : "任 务 失 败";
                SettlementTitleText.color = isVictory ? Color.green : Color.red;
            }
        }

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
            if (isVictory && GlobalProtocolRegistry.Instance != null)
            {
                GlobalProtocolRegistry.Instance.TickProtocolDurations();
            }
            if (GlobalResourceManager.Instance != null) GlobalResourceManager.Instance.ModifySAN(-totalSanLoss);
        }
        if (isVictory)
        {
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Combat_Victory);
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
        // 🌟【核心修复点 1】：把切歌指令提到最前面，且放在判定之外
        // 只要执行“返回地图”逻辑，必须无条件切回地图 BGM
        MusicManager.Instance?.SwitchState(MusicState.Map);

        PerformFullCleanup();

        if (MapManager.Instance != null && currentNodeData != null)
        {
            MapManager.Instance.OnCombatVictory(currentNodeData);
            currentNodeData = null;
        }
        else
        {
            // 兜底逻辑：即使没有节点数据，也要把地图 UI 屏显打开
            if (MapManager.Instance != null) MapManager.Instance.MapUIPanel.SetActive(true);
        }

        CurrentLayout = null;
    }

    // --- 👇【核心新增】：工业级大扫除方法 ---
    private void SilentCleanup()
    {
        // 这个方法和 PerformFullCleanup 逻辑一模一样，只是不打印那行吓人的 Debug.Log
        ResetBattlefieldInternal();
    }

    public void PerformFullCleanup()
    {
        Debug.Log("<color=#FF00FF>【战区管理】当前作战环境已物理卸载，逻辑回滚至待命态。</color>");
        ResetBattlefieldInternal();
    }

    // 核心重置逻辑提取
    private void ResetBattlefieldInternal()
    {
        // --- 👇【核心新增 A】：逻辑归还 ---
        if (PlayerInventoryManager.Instance != null && PlayerInventoryManager.Instance.HangarUnits != null)
        {
            Debug.Log("<color=cyan>【系统】</color> 正在回收前线机甲权限，重置部署标志位...");
            foreach (var profile in PlayerInventoryManager.Instance.HangarUnits)
            {
                if (profile != null)
                {
                    // 强行拨回“未部署”状态，这样下一场战斗才能重新拖拽
                    profile.IsDeployed = false;
                }
            }
        }
        SimplePool.ClearPool();
        // --- 👇【核心新增 B】：UI 关灯 ---
        if (ActiveSkillUIManager.Instance != null)
        {
            ActiveSkillUIManager.Instance.ClearUI(); // 销毁所有技能格子
            ActiveSkillUIManager.Instance.SetVisibility(false); // 隐藏整个技能栏
        }

        if (NavHangarButton != null)
        {
            NavHangarButton.interactable = true;
        }

        if (NavWarehouseButton != null)
        {
            NavWarehouseButton.interactable = true;
        }

        // --- 以下是原有的物理清理逻辑 ---
        IsCombatActive = false;
        IsDeploymentPhase = false;
        wallsGenerated = false;
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
                // 在销毁前，先执行我们之前写的“满血复活”同步
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

        // 强制刷新一次机库 UI，让“已部署”的印章消失
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