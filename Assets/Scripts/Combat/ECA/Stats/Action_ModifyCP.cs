// --- START OF FILE Action_ModifyCP.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "ModifyCP", menuName = "Chimera Protocol/2. ECA 机制积木/资源 - 增减 CP (Modify CP)")]
public class Action_ModifyCP : ECAAction
{
    [Tooltip("正数回蓝，负数耗蓝。比如魔法武器填 -2，回蓝辅助填 +1")]
    public float Amount = -2f;

    [Tooltip("如果勾选，且 CP 不够扣时，会强行拦截后续的所有 ECA 动作！(常用于武器开火的强制耗蓝)")]
    public bool BlockIfInsufficient = true;

    public override void Execute(ECAContext context)
    {
        if (GlobalCPManager.Instance == null) return;

        // 尝试增减 CP
        bool success = GlobalCPManager.Instance.ModifyCP(Amount);

        // 如果是扣蓝，且没钱了，且开启了拦截！
        if (!success && Amount < 0 && BlockIfInsufficient)
        {
            // 👇【核心黑魔法】：强制熔断！
            // 告诉上下文：这次执行失败了，排在后面的积木（比如发射子弹、播放枪声）统统作废！
            context.ExecutionAborted = true;

            // 可选：播放一个“咔哒”的哑火音效
            Debug.LogWarning($"<color=#FF00FF>【魔力枯竭】</color> {context.SourceWeapon.WeaponName} 尝试开火失败！需要 {-Amount} CP，当前仅剩 {GlobalCPManager.Instance.CurrentCP:F1}。");
        }
    }
}