using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "TransferBuffOnDeath", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 死亡传染Buff")]
public class Action_TransferBuffOnDeath : ECAAction
{
    [Tooltip("要传染的特定 Buff (如: 烛火)")]
    public BuffDataSO BuffToTransfer;
    [Tooltip("传染比例 (0.5 = 传递 50% 的层数给下一个敌人)")]
    [Range(0.1f, 1f)] public float TransferRatio = 0.5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || BuffToTransfer == null) return;

        BuffManager myBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (myBuffMgr == null) return;

        // 1. 读取死者身上的层数
        int currentStacks = myBuffMgr.GetBuffStacks(BuffToTransfer.BuffID);
        if (currentStacks <= 0) return;

        // 2. 算折损
        int stacksToTransfer = Mathf.Max(1, Mathf.FloorToInt(currentStacks * TransferRatio));

        DamageReceiver myReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        if (myReceiver == null) return;

        // 3. 寻找最近的存活同僚
        var allReceivers = FindObjectsOfType<DamageReceiver>();
        var nearestAlly = allReceivers
            .Where(r => r.isEnemy == myReceiver.isEnemy && r.CurrentHP > 0 && r.transform != context.SourceEntity)
            .OrderBy(r => Vector3.Distance(context.SourceEntity.position, r.transform.position))
            .FirstOrDefault();

        // 4. 传染过去！
        if (nearestAlly != null)
        {
            BuffManager targetBuffMgr = nearestAlly.GetComponent<BuffManager>();
            if (targetBuffMgr != null)
            {
                for (int i = 0; i < stacksToTransfer; i++) targetBuffMgr.ApplyBuff(BuffToTransfer, context);
                Debug.Log($"<color=#FFA500>【烛火蔓延】</color> 死亡传递了 {stacksToTransfer} 层烛火给 {nearestAlly.name}！");
            }
        }
    }
}