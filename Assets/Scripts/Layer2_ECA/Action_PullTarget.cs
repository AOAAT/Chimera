using UnityEngine;

[CreateAssetMenu(fileName = "PullTarget", menuName = "Chimera/ECA Actions/Pull Target (拉取敌人)")]
public class Action_PullTarget : ECAAction
{
    [Header("物理参数")]
    [Tooltip("每次命中拉近的距离（会受沙盒度量衡影响）")]
    public float PullDistance = 2.0f;

    public override void Execute(ECAContext context)
    {
        // 👇【核心修复】：大道至简！只要目标存在，就直接拉过来！去掉了那个愚蠢的子弹判定。
        if (context.PrimaryTarget != null)
        {
            // 1. 计算引力方向：从“敌人所在位置”指向“爆炸中心/武器插槽”
            Vector3 pullDirection = (context.ImpactPoint - context.PrimaryTarget.position).normalized;

            // 2. 引入全局度量衡
            float distanceMultiplier = 1.0f;
            if (CombatSandbox.Instance != null)
            {
                distanceMultiplier = CombatSandbox.Instance.DistanceMultiplier;
            }
            float realPullDist = PullDistance * distanceMultiplier;

            // 3. 施加位移
            context.PrimaryTarget.position += pullDirection * realPullDist;

            // 4. 画一条骚气的青色引力线
            Debug.DrawLine(context.PrimaryTarget.position, context.ImpactPoint, Color.cyan, 0.2f);
        }
    }
}