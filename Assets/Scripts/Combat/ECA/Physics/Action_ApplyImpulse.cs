using UnityEngine;

[CreateAssetMenu(fileName = "ApplyImpulse", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 动能击退 (Push)")]
public class Action_ApplyImpulse : ECAAction
{
    [Header("=== 物理打击参数 ===")]
    [Tooltip("基础冲量大小 (将被目标的质量稀释)")]
    public float BaseImpulse = 200f;

    // --- Action_ApplyImpulse.cs 全量加固版 ---
    public override void Execute(ECAContext context)
    {
        // 1. 寻找根节点 (Root) 的 DamageReceiver
        if (context.PrimaryTarget == null) return;

        // 穿透子物体查找根部控制组件
        GameObject rootObj = context.PrimaryTarget.gameObject;
        var brain = rootObj.GetComponentInParent<EnemyBrain>();
        var player = rootObj.GetComponentInParent<ChimeraAIController>();

        // 2. 计算方向：从爆炸点/枪口指向目标中心
        Vector2 targetCenter = context.PrimaryTarget.position;
        // 如果有碰撞盒，取中心点更准确
        var col = context.PrimaryTarget.GetComponent<Collider2D>();
        if (col != null) targetCenter = col.bounds.center;

        Vector2 pushDir = (targetCenter - (Vector2)context.ImpactPoint).normalized;
        if (pushDir == Vector2.zero) pushDir = Random.insideUnitCircle.normalized;

        // 3. 适配全局度量衡
        float speedMult = CombatSandbox.GetSpeed(1f);
        float finalImpulse = BaseImpulse * speedMult;

        // 4. 执行分发
        if (brain != null) brain.ApplyImpulse(pushDir, finalImpulse);
        if (player != null) player.ApplyImpulse(pushDir, finalImpulse, false);

        // 5. 调试：画一条红色的推力线
        Debug.DrawRay(context.ImpactPoint, pushDir * 2f, Color.red, 0.5f);
    }
}