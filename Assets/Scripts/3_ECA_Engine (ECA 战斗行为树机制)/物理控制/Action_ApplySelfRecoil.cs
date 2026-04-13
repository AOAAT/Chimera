// --- START OF FILE Action_ApplySelfRecoil.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "ApplySelfRecoil", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 开火后坐力 (Self Recoil)")]
public class Action_ApplySelfRecoil : ECAAction
{
    [Tooltip("后坐力冲量大小。会被机甲的总 Mass(质量) 稀释")]
    public float RecoilForce = 500f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || context.PrimaryTarget == null) return;

        // 1. 算出开火方向 (机甲 -> 目标)
        Vector2 fireDir = (context.PrimaryTarget.position - context.SourceEntity.position).normalized;

        // 2. 后坐力方向 = 开火方向的反方向！
        Vector2 recoilDir = -fireDir;

        // 3. 直接调用自己身上的 AI 控制器，给自己施加物理冲量！
        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI != null)
        {
            myAI.ApplyImpulse(recoilDir, RecoilForce);
        }
    }
}