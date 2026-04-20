using UnityEngine;

[CreateAssetMenu(fileName = "ApplySelfRecoil", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 开火后坐力 (Self Recoil)")]
public class Action_ApplySelfRecoil : ECAAction
{
    [Header("=== 物理参数 ===")]
    [Tooltip("后坐力冲量。建议数值：轻型枪 300，重型霰弹枪 1200+")]
    public float RecoilForce = 500f;

    [Tooltip("射击僵直时间 (秒)。期间机甲无法移动。建议：0.1 ~ 0.3")]
    public float FireStunDuration = 0.2f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || context.PrimaryTarget == null) return;

        // 计算反向
        Vector2 fireDir = (context.PrimaryTarget.position - context.SourceEntity.position).normalized;
        Vector2 recoilDir = -fireDir;

        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI != null)
        {
            // 👇【调用新接口】：带上你设置的僵直时间
            myAI.ApplyRecoil(recoilDir, RecoilForce, FireStunDuration);
        }

        // 震屏增强
        if (ScreenEffectManager.Instance != null && RecoilForce > 200)
        {
            ScreenEffectManager.Instance.TriggerShake(RecoilForce / 3000f, 0.1f);
        }
    }
}