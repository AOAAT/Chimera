using UnityEngine;

[CreateAssetMenu(fileName = "ParabellumLogic_V4", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 帕拉贝伦(极致精简版)")]
public class Action_ParabellumLogic : ECAAction
{
    public BuffDataSO DeepenBuff;
    public int StacksOnHit = 1;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || DeepenBuff == null) return;

        BuffManager mgr = context.SourceEntity.GetComponent<BuffManager>();
        if (mgr != null)
        {
            // 每次命中，给自己叠加 N 层伤害加深
            for (int i = 0; i < StacksOnHit; i++)
            {
                mgr.ApplyBuff(DeepenBuff, context);
            }
        }
    }
}