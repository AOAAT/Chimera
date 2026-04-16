using UnityEngine;

[CreateAssetMenu(fileName = "ConsumeAllCPForBuff", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 榨干CP换取Buff层数")]
public class Action_ConsumeAllCPForBuff : ECAAction
{
    [Tooltip("要给机甲挂载的伤害加深 Buff（必须是可线性叠层的）")]
    public BuffDataSO BuffToApply;
    [Tooltip("每 1 点 CP，转化为多少层 Buff？")]
    public int StacksPerCP = 1;

    public override void Execute(ECAContext context)
    {
        if (GlobalCPManager.Instance == null || BuffToApply == null || context.SourceEntity == null) return;

        // 1. 查余额
        int currentCP = Mathf.FloorToInt(GlobalCPManager.Instance.CurrentCP);
        if (currentCP <= 0) return;

        // 2. 扣干余额
        GlobalCPManager.Instance.ModifyCP(-currentCP);

        // 3. 计算层数并强行挂载
        BuffManager targetBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (targetBuffMgr != null)
        {
            int totalStacks = currentCP * StacksPerCP;
            for (int i = 0; i < totalStacks; i++)
            {
                targetBuffMgr.ApplyBuff(BuffToApply, context);
            }
            Debug.Log($"<color=#FF00FF>【全知之眼】</color> 献祭了 {currentCP} 点战术能量，获得了 {totalStacks} 层伤害加深！");
        }
    }
}