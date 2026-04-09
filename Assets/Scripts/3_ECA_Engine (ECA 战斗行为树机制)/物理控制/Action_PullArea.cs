// --- START OF FILE Action_PullArea.cs ---
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "PullArea", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 黑洞牵引 (Blackhole)")]
public class Action_PullArea : ECAAction
{
    public float PullRadius = 8f;
    public float PullForce = 800f; // 吸力极其巨大

    public override void Execute(ECAContext context)
    {
        float realRadius = PullRadius * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);

        // 找到爆炸点周围所有的单位 (不分敌我，黑洞是无情的！)
        var targets = FindObjectsOfType<DamageReceiver>()
            .Where(r => r.CurrentHP > 0)
            .Where(r => Vector3.Distance(context.ImpactPoint, r.transform.position) <= realRadius);

        foreach (var t in targets)
        {
            Vector2 pullDir = (context.ImpactPoint - t.transform.position).normalized;

            // 尝试呼叫它们的物理引擎！
            EnemyBrain enemy = t.GetComponent<EnemyBrain>();
            if (enemy != null) enemy.ApplyImpulse(pullDir, PullForce);

            // 如果连机甲都在范围内，机甲也会被吸过去！
            ChimeraAIController player = t.GetComponent<ChimeraAIController>();
            if (player != null) player.ApplyImpulse(pullDir, PullForce);
        }
    }
}