using UnityEngine;

[CreateAssetMenu(fileName = "SacrificeCP", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - CP献祭增伤")]
public class Action_SacrificeCPForBuff : ECAAction
{
    public BuffDataSO DeepenBuff;
    public float StacksPerCP = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (GlobalCPManager.Instance == null || context.SourceEntity == null || DeepenBuff == null) return;

        float currentCP = GlobalCPManager.Instance.CurrentCP;
        if (currentCP < 1.0f) return;

        GlobalCPManager.Instance.ModifyCP(-currentCP);
        int stacksToAdd = Mathf.FloorToInt(currentCP * StacksPerCP);

        BuffManager mgr = context.SourceEntity.GetComponent<BuffManager>();
        if (mgr != null)
        {
            // 直接循环施加 Buff 即可，BuffManager 会自动处理属性叠加
            for (int i = 0; i < stacksToAdd; i++)
            {
                mgr.ApplyBuff(DeepenBuff, context);
            }
        }
    }
}