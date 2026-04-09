// --- START OF FILE Action_RampUpStats.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "RampUpStats", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 越战越勇 (Ramp Up)")]
public class Action_RampUpStats : ECAAction
{
    [Header("=== 狂暴配置 ===")]
    [Tooltip("每次触发，增加多少点基础攻速？")]
    public float AttackSpeedBonus = 5f;

    [Tooltip("每次触发，增加多少点引擎出力(移速)？")]
    public float EnginePowerBonus = 2f;

    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null) return;

        // 1. 狂暴：永久增加这台机甲在这场战斗中的全局属性！
        context.ChassisData.GlobalStats[StatType.EnginePower] += EnginePowerBonus;

        // 2. 让武器也越来越快
        if (context.SourceWeapon != null)
        {
            if (context.SourceWeapon.WeaponStats.ContainsKey(StatType.AttackSpeed))
                context.SourceWeapon.WeaponStats[StatType.AttackSpeed] += AttackSpeedBonus;
            else
                context.SourceWeapon.WeaponStats.Add(StatType.AttackSpeed, AttackSpeedBonus);
        }

        // 3. 极其关键：必须通知底层 AI 重新计算移速！
        if (context.SourceEntity != null)
        {
            ChimeraAIController ai = context.SourceEntity.GetComponent<ChimeraAIController>();
            if (ai != null)
            {
                // 重新读取狂暴后的 EnginePower 计算移速
                float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
                ai.CurrentSpeed = GameFormulas.CalcMoveSpeed(context.ChassisData.GetGlobalStat(StatType.EnginePower), context.ChassisData.TotalMass, speedMult);
            }
        }

        Debug.Log($"<color=#FF4500>【狂暴】</color> 攻速飙升至 {context.SourceWeapon.GetStat(StatType.AttackSpeed)}，移速飙升！");
    }
}