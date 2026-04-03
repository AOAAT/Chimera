using UnityEngine;

[CreateAssetMenu(fileName = "Action_DealFlatDamage", menuName = "Chimera Protocol/ECA Actions/Deal Flat Damage (造成固定/真实伤害)")]
public class Action_DealFlatDamage : ECAAction
{
    [Header("=== 伤害设置 ===")]
    [Tooltip("基础固定伤害值（不依赖武器面板的独立伤害）")]
    public float FlatDamage = 50f;

    [Tooltip("是否为真实伤害？（勾选后，无视目标的 AP 护甲，直接扣除 HP）")]
    public bool IsTrueDamage = false;

    [Header("=== 表现设置 ===")]
    [Tooltip("UI 飘字或日志里显示的伤害来源名称（如：射钉爆甲、剧毒腐蚀）")]
    public string DamageSourceName = "机制附带伤害";

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        // 顺藤摸瓜找到目标身上的血条
        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            // 兜底获取武器名称，如果配置了自定义名称，优先用自定义的！
            string sourceName = !string.IsNullOrEmpty(DamageSourceName) ? DamageSourceName :
                                (context.SourceWeapon != null ? context.SourceWeapon.WeaponName : "未知机制");

            if (IsTrueDamage)
            {
                // 👇【核心功能】：真实伤害！无视 AP，直接调用 HP 扣除逻辑！
                // (注意：这里我们通过传递一个带有 "TrueDamage" 标签的 SourceName，
                // 让 DamageReceiver 能够识别并执行跳过护甲的逻辑。
                // 稍后我们需要微调一下 DamageReceiver 的代码来支持这个标签)

                string trueDamageTag = sourceName + " <color=#FF00FF>(真实伤害)</color>";

                // 暂时直接调用现有的 TakeDamage，等下我们教 Receiver 怎么跳过护甲
                receiver.TakeDamage(FlatDamage, trueDamageTag, isTrueDamage: true);
            }
            else
            {
                // 普通的固定物理伤害，依然会先打护甲
                receiver.TakeDamage(FlatDamage, sourceName);
            }
        }
    }
}