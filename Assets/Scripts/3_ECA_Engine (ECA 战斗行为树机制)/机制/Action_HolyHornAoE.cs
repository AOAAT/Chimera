using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HolyHornAoE", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 圣音号角(距离衰减+击杀回蓝)")]
public class Action_HolyHornAoE : ECAAction
{
    [Header("=== 范围与衰减 ===")]
    public float MaxRadius = 5.0f;
    [Tooltip("贴脸时的额外伤害倍率 (例如 3.0 代表 0 距离时伤害翻 3 倍)")]
    public float MaxProximityMultiplier = 3.0f;

    [Header("=== 击杀奖赏 ===")]
    public float CPRecoverOnKill = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 1. 确定中心点和真实半径
        Vector3 center = context.SourceEntity.position;
        float realRadius = CombatSandbox.GetDist(MaxRadius);

        // 2. 检索范围内敌人
        var targets = CombatDirector.ActiveEnemies
            .Where(e => e != null && e.CurrentHP > 0 && Vector3.Distance(center, e.transform.position) <= realRadius)
            .ToList();

        if (targets.Count == 0) return;

        int totalKills = 0;

        foreach (var t in targets)
        {
            // 3. 👇【核心算法】：计算距离倍率
            float dist = Vector3.Distance(center, t.transform.position);
            // 归一化距离 (0是贴脸, 1是边缘)
            float t_dist = Mathf.Clamp01(dist / realRadius);
            // 线性插值倍率：贴脸时最大, 边缘时 1.0
            float distMult = Mathf.Lerp(MaxProximityMultiplier, 1.0f, t_dist);

            // 4. 执行真实伤害
            float finalDmg = context.BaseDamage * context.TemporaryDamageModifier * distMult;

            // 记录击杀
            float hpBefore = t.CurrentHP;
            t.TakeDamage(finalDmg, "圣音号角", true); // 👈 强制 TrueDamage

            if (hpBefore > 0 && t.CurrentHP <= 0)
            {
                totalKills++;
            }
        }

        // 5. 击杀回馈：哪怕一嗓子震死 5 个，我们也老老实实回蓝
        if (totalKills > 0 && GlobalCPManager.Instance != null)
        {
            float totalRecover = totalKills * CPRecoverOnKill;
            GlobalCPManager.Instance.ModifyCP(totalRecover);
            Debug.Log($"<color=#FFD700>【圣音收割】</color> 震碎了 {totalKills} 个单位，回收能量 {totalRecover:F1}");

            // 击杀成功时，来一个强力的卡肉反馈
            if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.12f, 0.01f);
        }

        // 6. 视觉反馈：圣音震波
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.3f, 0.2f);
    }
}