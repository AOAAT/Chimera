using UnityEngine;

[CreateAssetMenu(fileName = "ParabellumLogic", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 帕拉贝伦增伤")]
public class Action_ParabellumLogic : ECAAction
{
    [Header("=== 数值公式配置 ===")]
    [Tooltip("加深比例。例如 0.1 代表每次命中增加 (暴击率 * 最大攻击力 * 0.1) 的伤害")]
    public float GrowthRatio = 0.2f;

    [Tooltip("加成是否在换弹后重置？勾选则每 3 发一个循环；不勾选则整场战斗持续变强")]
    public bool ResetOnReload = false;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        var weapon = context.SourceWeapon;
        var states = weapon.CustomStates;

        // 1. 【核心公式计算】
        // 读取当前武器的实时面板（含 Buff 加成）
        float critChance = weapon.GetStat(StatType.CriticalChance);
        float maxDamage = weapon.GetStat(StatType.MaxDamage);

        // 增量 I = (Crit * MaxDmg) * Ratio
        float increment = (critChance * maxDamage) * GrowthRatio;

        // 2. 【状态追踪】
        if (!states.ContainsKey("ParabellumAccumulated")) states["ParabellumAccumulated"] = 0f;

        // 判定是否刚完成一轮换弹（配合 MagazineControl 积木使用）
        // 如果 MagazineControl 刚重置了弹药，我们在此检查是否需要重置伤害
        if (ResetOnReload && states.ContainsKey("CurrentAmmo"))
        {
            // 如果当前是满弹状态且还没射击（此时由 MagazineControl 刚把 0 变成 MaxAmmo）
            // 这里需要一个逻辑标记来判定“第一发”
            // 为了简化，我们假设逻辑是持续叠加的，更符合“日出”的主题
        }

        // 3. 【应用加成】
        // 获取之前累积的伤害，并附加到本次 Context 的 BaseDamage 上
        float currentBonus = states["ParabellumAccumulated"];
        context.BaseDamage += currentBonus;

        // 4. 【杀意进化】
        // 为下一次攻击积攒力量
        states["ParabellumAccumulated"] = currentBonus + increment;

        Debug.Log($"<color=#FFD700>【帕拉贝伦】</color> 本次加成: {currentBonus:F1} | 下次增量: {increment:F1}");
    }
}