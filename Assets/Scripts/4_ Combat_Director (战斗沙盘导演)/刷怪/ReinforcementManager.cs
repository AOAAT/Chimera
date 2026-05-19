// --- 修改 ReinforcementManager.cs ---
using System.Collections.Generic;
using UnityEngine;
using static ReinforcementConfigSO;

public class ReinforcementManager : MonoBehaviour
{
    public static ReinforcementManager Instance;

    private ReinforcementConfigSO config;
    private int currentPhaseIndex = 0;
    private float phaseTimer = 0f;
    private float spawnTimer = 0f;
    private bool isActive = false;
    private bool allPhasesFinished = false;

    public bool IsAllPhasesFinished => allPhasesFinished;
    public int CurrentPhaseDisplay => currentPhaseIndex + 1;

    private void Awake() => Instance = this;

    public void StartTimeline(ReinforcementConfigSO data)
    {
        if (data == null || data.Phases.Count == 0) return;

        config = data;
        currentPhaseIndex = 0;
        allPhasesFinished = false;
        isActive = true;

        LoadPhase(0);
        Debug.Log($"<color=#FF4500>【增援总署】战斗序列启动，共 {config.Phases.Count} 个阶段。</color>");
    }

    public void StopTimeline() => isActive = false;

    private void LoadPhase(int index)
    {
        if (index >= config.Phases.Count)
        {
            allPhasesFinished = true;
            isActive = false;
            return;
        }

        currentPhaseIndex = index;
        BattlePhase phase = config.Phases[currentPhaseIndex];
        phaseTimer = phase.Duration;
        spawnTimer = 2.0f; // 每个阶段载入后，2秒后进行第一次刷怪尝试

        Debug.Log($"<color=#FFA500>【阶段切换】进入阶段 {CurrentPhaseDisplay}: {phase.PhaseName} (持续{phase.Duration}s)</color>");
    }

    private void Update()
    {
        if (!isActive || config == null || allPhasesFinished) return;
        if (CombatDirector.Instance == null || !CombatDirector.Instance.IsCombatActive) return;

        BattlePhase currentPhase = config.Phases[currentPhaseIndex];

        // 1. 阶段计时
        phaseTimer -= Time.deltaTime;
        spawnTimer -= Time.deltaTime;

        // --- 👇 核心逻辑：阶段提前跳跃 (Phase Skip) ---
        // 如果当前阶段还有时间，但场上怪已经没了，且不是最后一波
        if (phaseTimer > 0.5f && CombatDirector.ActiveEnemies.Count == 0)
        {
            Debug.Log("<color=cyan>【战术压制】场上敌人已被清空，强制跳跃至下一阶段！</color>");
            GoToNextPhase();
            return;
        }

        // 2. 正常时间耗尽跳转
        if (phaseTimer <= 0)
        {
            GoToNextPhase();
            return;
        }

        // 3. 刷怪逻辑
        if (spawnTimer <= 0)
        {
            TrySpawnWave(currentPhase);
            spawnTimer = currentPhase.SpawnInterval;
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
            Debug.Log("<color=green>【增援总署】所有战斗序列投递完毕，等待最终清场。</color>");
        }
    }

    private void TrySpawnWave(BattlePhase phase)
    {
        if (CombatDirector.ActiveEnemies.Count >= phase.MaxEnemiesOnField) return;

        int quota = phase.MaxEnemiesOnField - CombatDirector.ActiveEnemies.Count;
        int spawnCount = Mathf.Min(phase.MaxPerSpawn, quota);

        if (spawnCount <= 0 || phase.PhaseEnemyPool.Count == 0) return;

        Vector2 arenaSize = CombatDirector.Instance.CurrentArenaSize;
        Vector3 arenaCenter = CombatDirector.Instance.CurrentArenaCenter;

        for (int i = 0; i < spawnCount; i++)
        {
            // 1. 确定这一只怪到底从哪边出来
            SpawnSide actualSide = phase.Direction;
            if (actualSide == SpawnSide.RandomFourSides)
                actualSide = (SpawnSide)Random.Range(0, 4);
            else if (actualSide == SpawnSide.Horizontal)
                actualSide = Random.value > 0.5f ? SpawnSide.Left : SpawnSide.Right;
            else if (actualSide == SpawnSide.Vertical)
                actualSide = Random.value > 0.5f ? SpawnSide.Top : SpawnSide.Bottom;

            // 2. 解算坐标与进场冲力方向
            Vector3 spawnPos = Vector3.zero;
            Vector2 impulseDir = Vector2.zero;

            float margin = 1.0f; // 距离边缘的内缩距离

            switch (actualSide)
            {
                case SpawnSide.Right:
                    spawnPos = new Vector3(arenaCenter.x + arenaSize.x / 2f - margin, arenaCenter.y + Random.Range(-arenaSize.y / 3f, arenaSize.y / 3f), 0);
                    impulseDir = Vector2.left;
                    break;
                case SpawnSide.Left:
                    spawnPos = new Vector3(arenaCenter.x - arenaSize.x / 2f + margin, arenaCenter.y + Random.Range(-arenaSize.y / 3f, arenaSize.y / 3f), 0);
                    impulseDir = Vector2.right;
                    break;
                case SpawnSide.Top:
                    spawnPos = new Vector3(arenaCenter.x + Random.Range(-arenaSize.x / 3f, arenaSize.x / 3f), arenaCenter.y + arenaSize.y / 2f - margin, 0);
                    impulseDir = Vector2.down;
                    break;
                case SpawnSide.Bottom:
                    spawnPos = new Vector3(arenaCenter.x + Random.Range(-arenaSize.x / 3f, arenaSize.x / 3f), arenaCenter.y - arenaSize.y / 2f + margin, 0);
                    impulseDir = Vector2.up;
                    break;
            }

            // 3. 执行刷新
            EnemyDataSO randomEnemy = phase.PhaseEnemyPool[Random.Range(0, phase.PhaseEnemyPool.Count)];
            SpawnEntity(randomEnemy, spawnPos, impulseDir);
        }
    }

    private void SpawnEntity(EnemyDataSO data, Vector3 pos, Vector2 impulseDir)
    {
        GameObject enemyObj;
        if (data.Archetype == EnemyArchetype.Modular)
        {
            enemyObj = Instantiate(CombatDirector.Instance.ModularEnemyPrefab, pos, Quaternion.identity);
            enemyObj.GetComponent<MechUnit2D>()?.InitAsEliteEnemy(data);
        }
        else
        {
            enemyObj = Instantiate(CombatDirector.Instance.BaseEnemyPrefab, pos, Quaternion.identity);
            enemyObj.GetComponent<EnemyBrain>().MyData = data;
        }

        Rigidbody2D rb = enemyObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 使用传入的方向施加冲力
            rb.AddForce(impulseDir * 5f, ForceMode2D.Impulse);
        }
    }
    public bool IsTimelineFinished => allPhasesFinished;

    // 逻辑进度算法：算出当前在总阶段中的进度 (0.0 ~ 1.0)
    public float Progress
    {
        get
        {
            if (config == null || config.Phases.Count == 0) return 1f;
            if (allPhasesFinished) return 1f;

            float totalPhases = config.Phases.Count;
            float completedPhasesBase = (float)currentPhaseIndex / totalPhases;

            // 算出当前阶段内部走过的比例
            float currentPhaseDuration = config.Phases[currentPhaseIndex].Duration;
            float currentPhasePercent = 1f - Mathf.Clamp01(phaseTimer / currentPhaseDuration);

            // 映射到全局总进度
            return completedPhasesBase + (currentPhasePercent / totalPhases);
        }
    }
}