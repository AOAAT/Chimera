using System.Collections.Generic;
using UnityEngine;

public enum EnemyTokenType
{
    HeavyAttack,    // 重击（如牛牛冲撞）
    CrowdControl,   // 控制（如引力拉取）
    RangedBarrage,  // 远程弹幕（如凝望者激光）
    Mobility,       // 特殊位移（如猎犬瞬移）
    Ultimate        // Boss大招
}

public class EnemyActionDirector : MonoBehaviour
{
    public static EnemyActionDirector Instance;

    [System.Serializable]
    public class TokenPool
    {
        public EnemyTokenType Type;
        public int MaxCapacity = 3;
        public int CurrentUsed = 0;
    }

    [Header("=== 全局令牌配额设置 ===")]
    public List<TokenPool> ConfiguredPools = new List<TokenPool>();

    private Dictionary<EnemyTokenType, TokenPool> poolLookup = new Dictionary<EnemyTokenType, TokenPool>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        foreach (var pool in ConfiguredPools)
        {
            poolLookup[pool.Type] = pool;
        }
    }

    /// <summary>
    /// 申请令牌：成功返回 true，否则返回 false
    /// </summary>
    public bool TryRequestToken(EnemyTokenType type)
    {
        if (!poolLookup.ContainsKey(type)) return true; // 如果没配这个类型的池子，默认不限制

        TokenPool pool = poolLookup[type];
        if (pool.CurrentUsed < pool.MaxCapacity)
        {
            pool.CurrentUsed++;
            // Debug.Log($"<color=orange>【令牌申请】</color> 类型:{type} | 剩余:{pool.MaxCapacity - pool.CurrentUsed}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 归还令牌
    /// </summary>
    public void ReturnToken(EnemyTokenType type)
    {
        if (!poolLookup.ContainsKey(type)) return;

        TokenPool pool = poolLookup[type];
        pool.CurrentUsed = Mathf.Max(0, pool.CurrentUsed - 1);
        // Debug.Log($"<color=cyan>【令牌归还】</color> 类型:{type} | 剩余:{pool.MaxCapacity - pool.CurrentUsed}");
    }
}