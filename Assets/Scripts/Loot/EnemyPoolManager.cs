// --- START OF FILE EnemyPoolManager.cs ---
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 最终输出给战斗引擎的数据
public class EnemyToSpawn
{
    public EnemyDataSO EnemyData;
}

[Serializable]
public class EnemySpawnDef
{
    public EnemyDataSO EnemyData;
    public int MinCount = 1;
    public int MaxCount = 3;
}

// ==========================================
// 单个敌人池配置单
// ==========================================
[CreateAssetMenu(fileName = "NewEnemyPool", menuName = "Chimera Protocol/Combat/Enemy Pool Config")]
public class EnemyPoolConfigSO : ScriptableObject
{
    [Header("=== 1. 基础权重 ===")]
    public float PoolWeight = 10f;

    [Header("=== 2. 筛选条件 (Filter Criteria) ===")]
    public int TargetStage = 1; // 适用的大阶段 (如 Stage 1)

    [Tooltip("适用的最小层数 (LayerIndex)")]
    public int MinDepth = 0;
    [Tooltip("适用的最大层数 (LayerIndex)")]
    public int MaxDepth = 15;

    // 👇【核心融合】：直接复用你地图里的 MapNodeType！
    [Tooltip("允许出现的节点类型")]
    public List<MapNodeType> AllowedNodeTypes = new List<MapNodeType> { MapNodeType.Enemy };

    [Tooltip("允许出现的主题 (Tech/Flesh)")]
    public List<NodeTheme> AllowedThemes = new List<NodeTheme> { NodeTheme.Tech };

    [Header("=== 3. 怪物构成 ===")]
    public List<EnemySpawnDef> Spawns = new List<EnemySpawnDef>();
}

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance;

    [Header("=== 全局怪物池数据库 ===")]
    public List<EnemyPoolConfigSO> GlobalPoolDatabase = new List<EnemyPoolConfigSO>();

    private void Awake() { if (Instance == null) Instance = this; }

    // ==========================================
    // 核心算法：四维环境解析与波次生成
    // ==========================================
    public List<EnemyToSpawn> GenerateWave(int currentStage, int currentLayer, MapNodeType nodeType, NodeTheme nodeTheme)
    {
        var validPools = GlobalPoolDatabase.Where(pool =>
            IsPoolValidForEnvironment(pool, currentStage, currentLayer, nodeType, nodeTheme)
        ).ToList();

        if (validPools.Count == 0)
        {
            Debug.LogError($"【刷怪异常】找不到任何匹配 Stage:{currentStage} | Layer:{currentLayer} | Type:{nodeType} | Theme:{nodeTheme} 的怪物池！");
            return new List<EnemyToSpawn>();
        }

        var selectedPool = PickPoolByWeight(validPools);
        return ParsePoolIntoSpawnList(selectedPool);
    }

    private bool IsPoolValidForEnvironment(EnemyPoolConfigSO pool, int stage, int layer, MapNodeType type, NodeTheme theme)
    {
        if (pool.TargetStage != stage) return false;
        if (layer < pool.MinDepth || layer > pool.MaxDepth) return false;

        // 👇 完美对接地图系统！
        if (!pool.AllowedNodeTypes.Contains(type)) return false;

        // 如果是精英怪或 Boss，跳过 Theme 检查 (Manual Override 特权)
        if (type == MapNodeType.Elite || type == MapNodeType.Boss) return true;

        if (!pool.AllowedThemes.Contains(theme)) return false;

        return true;
    }

    private EnemyPoolConfigSO PickPoolByWeight(List<EnemyPoolConfigSO> pools)
    {
        float totalWeight = pools.Sum(p => p.PoolWeight);
        float roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (var pool in pools)
        {
            if (roll < pool.PoolWeight) return pool;
            roll -= pool.PoolWeight;
        }
        return pools.Last();
    }

    private List<EnemyToSpawn> ParsePoolIntoSpawnList(EnemyPoolConfigSO pool)
    {
        List<EnemyToSpawn> finalRoster = new List<EnemyToSpawn>();
        foreach (var def in pool.Spawns)
        {
            int count = UnityEngine.Random.Range(def.MinCount, def.MaxCount + 1);
            for (int i = 0; i < count; i++) finalRoster.Add(new EnemyToSpawn { EnemyData = def.EnemyData });
        }
        return finalRoster;
    }
}