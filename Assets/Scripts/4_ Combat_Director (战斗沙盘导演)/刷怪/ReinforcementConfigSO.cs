using System.Collections.Generic;
using UnityEngine;

// --- 1. 刷新方位枚举 ---
public enum SpawnSide { Right, Left, Top, Bottom, RandomFourSides, Horizontal, Vertical }

// --- 2. 阶段数据结构 (移出类定义，确保全局身份一致) ---
[System.Serializable]
public class BattlePhase
{
    public string PhaseName = "新阶段";

    [Header("=== 刷新方位 ===")]
    public SpawnSide Direction = SpawnSide.Right;

    [Header("=== 时间参数 ===")]
    [Tooltip("本阶段持续的总秒数 (t)")]
    public float Duration = 20f;
    [Tooltip("刷怪的时间间隔 (S)")]
    public float SpawnInterval = 5f;

    [Header("=== 数量规模 ===")]
    [Tooltip("场上允许存在的最大敌人数 (X)")]
    public int MaxEnemiesOnField = 5;
    [Tooltip("每次刷怪的最大数量 (n)")]
    public int MaxPerSpawn = 2;

    [Header("=== 专属兵力池 ===")]
    public List<EnemyDataSO> PhaseEnemyPool = new List<EnemyDataSO>();
}

// --- 3. ScriptableObject 容器 ---
[CreateAssetMenu(fileName = "NewReinforcementData", menuName = "Chimera Protocol/3. 宏观控制/增援配置 (Reinforcement)")]
public class ReinforcementConfigSO : ScriptableObject
{
    [Header("=== 阶段序列配置 ===")]
    public List<BattlePhase> Phases = new List<BattlePhase>();
}