using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("=== 全局遭遇战牌库大合集 ===")]
    public List<EncounterPoolSO> GlobalPools = new List<EncounterPoolSO>();

    [Header("=== 运行时进度状态 ===")]
    public int CurrentStage = 1;

    private void Awake()
    {
        // 修改点：去掉了 DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // 👇【去掉了 Theme 参数】
    public EncounterLayoutSO GetNextEncounter(int stage, int layer, MapNodeType type)
    {
        var validPools = GlobalPools.Where(pool => IsPoolValid(pool, stage, layer, type)).ToList();

        if (validPools.Count == 0)
        {
            Debug.LogError($"【发牌错误】找不到匹配 Stage:{stage} | Layer:{layer} | Type:{type} 的遭遇战牌库！");
            return null;
        }

        var selectedPool = PickPoolByWeight(validPools);
        return selectedPool.GetNextEncounter();
    }

    // 👇【过滤逻辑极简】
    private bool IsPoolValid(EncounterPoolSO pool, int stage, int layer, MapNodeType type)
    {
        if (pool.TargetStage != stage) return false;
        if (layer < pool.MinDepth || layer > pool.MaxDepth) return false;

        // 只要节点类型在允许列表里，就通过！
        if (!pool.AllowedNodeTypes.Contains(type)) return false;

        return true;
    }

    private EncounterPoolSO PickPoolByWeight(List<EncounterPoolSO> pools)
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
}