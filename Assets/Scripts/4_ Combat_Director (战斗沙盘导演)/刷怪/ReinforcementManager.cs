using System.Collections.Generic;
using UnityEngine;
using static ReinforcementConfigSO;

public class ReinforcementManager : MonoBehaviour
{
    // --------------------------------------------------------
    // 🌟 核心升级：自动生成且跨场景永生的单例模式
    // --------------------------------------------------------
    private static ReinforcementManager _instance;
    public static ReinforcementManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ReinforcementManager>(true);

                if (_instance == null)
                {
                    GameObject go = new GameObject("[Auto-Generated] Reinforcement Manager");
                    _instance = go.AddComponent<ReinforcementManager>();

                    DontDestroyOnLoad(go);
                    Debug.Log("<color=yellow>[System] 检测到总署丢失，已通过代码自动重构 ReinforcementManager！</color>");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Destroy(this.gameObject);
        }
    }

    private ReinforcementConfigSO config;
    private int currentPhaseIndex = 0;
    private float phaseTimer = 0f;
    private float spawnTimer = 0f;
    private bool isActive = false;
    private bool allPhasesFinished = false;

    public bool IsAllPhasesFinished => allPhasesFinished;
    public int CurrentPhaseDisplay => currentPhaseIndex + 1;

    public void StartTimeline(ReinforcementConfigSO data)
    {
        if (data == null || data.Phases.Count == 0)
        {
            Debug.LogError("<color=red>[Spawn-Debug] 启动失败：传入的增援配置为空或没有阶段！</color>");
            return;
        }

        config = data;
        currentPhaseIndex = 0;
        allPhasesFinished = false;
        isActive = true;

        Debug.Log($"<color=#FF4500>[Spawn-Debug] 增援序列启动，共包含 {config.Phases.Count} 个阶段。</color>");
        LoadPhase(0);
    }

    public void StopTimeline() => isActive = false;

    private void LoadPhase(int index)
    {
        if (index >= config.Phases.Count)
        {
            allPhasesFinished = true;
            isActive = false;
            Debug.Log("<color=green>[Spawn-Debug] 所有阶段投递完毕！</color>");
            return;
        }

        currentPhaseIndex = index;
        BattlePhase phase = config.Phases[currentPhaseIndex];
        phaseTimer = phase.Duration;
        spawnTimer = 2.0f;
        Debug.Log($"<color=#FFA500>[Spawn-Debug] 载入阶段 {CurrentPhaseDisplay}: {phase.PhaseName}，将在2秒后首次刷怪。</color>");
    }

    private void Update()
    {
        if (!isActive || config == null || allPhasesFinished) return;
        if (CombatDirector.Instance == null || !CombatDirector.Instance.IsCombatActive) return;

        BattlePhase currentPhase = config.Phases[currentPhaseIndex];

        phaseTimer -= Time.deltaTime;
        spawnTimer -= Time.deltaTime;

        CombatDirector.ActiveEnemies.RemoveAll(e => e == null);

        if (phaseTimer > 0.5f && CombatDirector.ActiveEnemies.Count == 0)
        {
            GoToNextPhase();
            return;
        }

        if (phaseTimer <= 0)
        {
            GoToNextPhase();
            return;
        }

        if (spawnTimer <= 0)
        {
            spawnTimer = currentPhase.SpawnInterval;
            TrySpawnWave(currentPhase);
        }
    }

    private void GoToNextPhase()
    {
        if (currentPhaseIndex + 1 < config.Phases.Count)
        {
            LoadPhase(currentPhaseIndex + 1);
        }
        else
        {
            allPhasesFinished = true;
        }
    }

    private void TrySpawnWave(BattlePhase phase)
    {
        if (CombatDirector.ActiveEnemies.Count >= phase.MaxEnemiesOnField) return;

        int quota = phase.MaxEnemiesOnField - CombatDirector.ActiveEnemies.Count;
        int spawnCount = Mathf.Min(phase.MaxPerSpawn, quota);

        var validEnemies = phase.PhaseEnemyPool.FindAll(e => e != null);
        if (spawnCount <= 0 || validEnemies.Count == 0)
        {
            Debug.LogWarning($"<color=orange>[Spawn-Debug] 警告：需要刷怪但池子里没有合法配置！有效敌人数量: {validEnemies.Count}</color>");
            return;
        }

        Debug.Log($"<color=#00FFFF>[Spawn-Debug] 准备投放波次！计划投放 {spawnCount} 只，当前场上 {CombatDirector.ActiveEnemies.Count} 只。</color>");

        Vector2 arenaSize = CombatDirector.Instance.CurrentArenaSize;
        Vector3 arenaCenter = CombatDirector.Instance.CurrentArenaCenter;

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnSide actualSide = phase.Direction;
            if (actualSide == SpawnSide.RandomFourSides) actualSide = (SpawnSide)Random.Range(0, 4);
            else if (actualSide == SpawnSide.Horizontal) actualSide = Random.value > 0.5f ? SpawnSide.Left : SpawnSide.Right;
            else if (actualSide == SpawnSide.Vertical) actualSide = Random.value > 0.5f ? SpawnSide.Top : SpawnSide.Bottom;

            Vector3 spawnPos = Vector3.zero;
            Vector2 impulseDir = Vector2.zero;
            float margin = 2.5f;

            switch (actualSide)
            {
                case SpawnSide.Right:
                    spawnPos = new Vector3(arenaCenter.x + arenaSize.x / 2f - margin, arenaCenter.y + Random.Range(-arenaSize.y / 4f, arenaSize.y / 4f), 0);
                    impulseDir = Vector2.left;
                    break;
                case SpawnSide.Left:
                    spawnPos = new Vector3(arenaCenter.x - arenaSize.x / 2f + margin, arenaCenter.y + Random.Range(-arenaSize.y / 4f, arenaSize.y / 4f), 0);
                    impulseDir = Vector2.right;
                    break;
                case SpawnSide.Top:
                    spawnPos = new Vector3(arenaCenter.x + Random.Range(-arenaSize.x / 4f, arenaSize.x / 4f), arenaCenter.y + arenaSize.y / 2f - margin, 0);
                    impulseDir = Vector2.down;
                    break;
                case SpawnSide.Bottom:
                    spawnPos = new Vector3(arenaCenter.x + Random.Range(-arenaSize.x / 4f, arenaSize.x / 4f), arenaCenter.y - arenaSize.y / 2f + margin, 0);
                    impulseDir = Vector2.up;
                    break;
            }

            EnemyDataSO randomEnemy = validEnemies[Random.Range(0, validEnemies.Count)];
            SpawnEntity(randomEnemy, spawnPos, impulseDir);
        }
    }

    private void SpawnEntity(EnemyDataSO data, Vector3 pos, Vector2 impulseDir)
    {
        if (data == null) return;
        Debug.Log($"<color=#FFFF00>[Spawn-Debug] 正在实例化: {data.EnemyName} 于坐标 {pos}</color>");

        GameObject enemyObj = null;

        try
        {
            if (data.Archetype == EnemyArchetype.Modular)
            {
                if (CombatDirector.Instance.ModularEnemyPrefab == null) Debug.LogError("<color=red>[Spawn-Debug] 致命错误：ModularEnemyPrefab 丢失！</color>");
                enemyObj = Instantiate(CombatDirector.Instance.ModularEnemyPrefab, pos, Quaternion.identity);
                enemyObj.GetComponent<MechUnit2D>()?.InitAsEliteEnemy(data);
            }
            else
            {
                if (CombatDirector.Instance.BaseEnemyPrefab == null) Debug.LogError("<color=red>[Spawn-Debug] 致命错误：BaseEnemyPrefab 丢失！</color>");
                enemyObj = Instantiate(CombatDirector.Instance.BaseEnemyPrefab, pos, Quaternion.identity);
                EnemyBrain brain = enemyObj.GetComponent<EnemyBrain>();
                if (brain != null) brain.MyData = data;
                else Debug.LogError($"<color=red>[Spawn-Debug] 预制体上缺少 EnemyBrain 组件！</color>");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[Spawn-Debug] 实例化过程抛出异常: {e.Message}</color>");
            return;
        }

        if (enemyObj == null) return;

        StartCoroutine(PhaseEntryRoutine(enemyObj));

        Rigidbody2D rb = enemyObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(impulseDir * 8f, ForceMode2D.Impulse);
            Debug.Log($"<color=cyan>[Spawn-Debug] 已对 {data.EnemyName} 施加入场推力。</color>");
        }

        DamageReceiver newReceiver = enemyObj.GetComponent<DamageReceiver>();
        if (newReceiver != null && CombatDirector.ActiveEnemies != null)
        {
            CombatDirector.ActiveEnemies.Add(newReceiver);
            Debug.Log($"<color=green>[Spawn-Debug] 户口登记成功！当前场上敌人总数: {CombatDirector.ActiveEnemies.Count}</color>");
        }
    }

    private System.Collections.IEnumerator PhaseEntryRoutine(GameObject unit)
    {
        if (unit == null) yield break;

        Collider2D physCol = unit.GetComponent<Collider2D>();
        if (physCol != null)
        {
            bool originalTriggerState = physCol.isTrigger;
            physCol.isTrigger = true;

            yield return new WaitForSeconds(0.5f);

            if (physCol != null) physCol.isTrigger = originalTriggerState;
        }
    }
    public bool IsTimelineFinished => allPhasesFinished;
    public float Progress
    {
        get
        {
            if (config == null || config.Phases.Count == 0) return 1f;
            if (allPhasesFinished) return 1f;

            float totalPhases = config.Phases.Count;
            float completedPhasesBase = (float)currentPhaseIndex / totalPhases;

            float currentPhaseDuration = config.Phases[currentPhaseIndex].Duration;
            float currentPhasePercent = 1f - Mathf.Clamp01(phaseTimer / currentPhaseDuration);

            return completedPhasesBase + (currentPhasePercent / totalPhases);
        }
    }
}