using UnityEngine;

[CreateAssetMenu(fileName = "MeleeCombo", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 近战武器连携")]
public class Action_MeleeCombo : ECAAction
{
    public float Cooldown = 0.5f;

    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null || context.SourceWeapon == null) return;

        // 防止死循环递归：检查该 Context 是否已经是由 Combo 触发的
        if (context.CustomStates.ContainsKey("IsComboAttack")) return;

        // 寻找除了自己以外的第一个近战武器
        var otherMelee = context.ChassisData.EquippedWeapons.Find(w =>
            w.DeliveryType == WeaponDeliveryType.Melee && w != context.SourceWeapon);

        if (otherMelee != null)
        {
            Debug.Log($"【猩猩手臂】触发连携：{otherMelee.WeaponName}！");

            // 复制一个上下文并标记
            ECAContext comboCtx = context;
            comboCtx.CustomStates["IsComboAttack"] = 1.0f;

            // 这种逻辑通常需要通过发射一个不可见的“Combo子弹”或直接调用 HitActions
            foreach (var hitAction in otherMelee.OnHitActions)
            {
                hitAction.Execute(comboCtx);
            }
        }
    }
}