using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "MeleeCombo", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 近战武器连携")]
public class Action_MeleeCombo : ECAAction
{
    public float ComboCooldown = 0.5f;
    public float ComboDamageMultiplier = 0.8f;

    public override void Execute(ECAContext context)
    {
        // 👇【核心防御】：如果当前上下文已经标记为“连携中”，立即熔断，严禁套娃！
        if (context.CustomStates.ContainsKey("IsMeleeComboInProgress"))
        {
            return;
        }

        if (context.ChassisData == null || context.SourceWeapon == null || context.PrimaryTarget == null) return;

        var currentWeapon = context.SourceWeapon;
        var states = currentWeapon.CustomStates;

        if (states.ContainsKey("MeleeComboTimer") && Time.time < states["MeleeComboTimer"]) return;

        var otherMeleeWeapons = context.ChassisData.EquippedWeapons.Where(w =>
            w.DeliveryType == WeaponDeliveryType.Melee && w != currentWeapon
        ).ToList();

        if (otherMeleeWeapons.Count == 0) return;

        RuntimeWeapon targetWeapon = otherMeleeWeapons[Random.Range(0, otherMeleeWeapons.Count)];

        // 构建新的上下文
        ECAContext comboContext = new ECAContext
        {
            ImpactPoint = context.ImpactPoint,
            PrimaryTarget = context.PrimaryTarget,
            BaseDamage = context.BaseDamage * ComboDamageMultiplier,
            SourceWeapon = targetWeapon,
            ChassisData = context.ChassisData,
            IsEnemyFire = context.IsEnemyFire,
            IsCriticalHit = context.IsCriticalHit,
            SourceEntity = context.SourceEntity
        };

        // 👇【核心防御】：打上标签，告诉下一层积木：“你是被带出来的，不准再带别人！”
        comboContext.CustomStates["IsMeleeComboInProgress"] = 1.0f;

        if (targetWeapon.OnHitActions != null)
        {
            foreach (var action in targetWeapon.OnHitActions)
            {
                if (action != null) action.Execute(comboContext);
            }
        }

        states["MeleeComboTimer"] = Time.time + ComboCooldown;
    }
}