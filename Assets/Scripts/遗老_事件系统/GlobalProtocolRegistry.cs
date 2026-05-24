using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActiveProtocol
{
    public BuffDataSO ProtocolBuff;
    public int RemainingBattles; // 剩余场次
}

public class GlobalProtocolRegistry : MonoBehaviour
{
    public static GlobalProtocolRegistry Instance;

    [Header("=== 当前激活的全局协议 ===")]
    public List<ActiveProtocol> ActiveProtocols = new List<ActiveProtocol>();

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

    // 由事件积木调用
    public void AddProtocol(BuffDataSO buff, int duration)
    {
        ActiveProtocols.Add(new ActiveProtocol { ProtocolBuff = buff, RemainingBattles = duration });
        Debug.Log($"<color=#00FFFF>【协议录入】</color> 已激活: {buff.BuffName}，持续 {duration} 场战斗。");
    }

    // 由 CombatDirector 在战斗开始时调用
    public void ApplyProtocolsToUnits(List<DamageReceiver> players)
    {
        foreach (var unit in players)
        {
            BuffManager bm = unit.GetComponent<BuffManager>();
            if (bm == null) continue;

            foreach (var proto in ActiveProtocols)
            {
                // 构造一个简单的上下文，标记来源为“系统协议”
                ECAContext ctx = new ECAContext { SourceEntity = this.transform, IsEnemyFire = false };
                bm.ApplyBuff(proto.ProtocolBuff, ctx);
            }
        }
    }

    // 由 CombatDirector 在战斗结束结算时调用
    public void TickProtocolDurations()
    {
        for (int i = ActiveProtocols.Count - 1; i >= 0; i--)
        {
            ActiveProtocols[i].RemainingBattles--;
            if (ActiveProtocols[i].RemainingBattles <= 0)
            {
                Debug.Log($"<color=yellow>【协议过期】</color> {ActiveProtocols[i].ProtocolBuff.BuffName} 已失效。");
                ActiveProtocols.RemoveAt(i);
            }
        }
    }
}