// --- START OF FILE GameFormulas.cs ---
using UnityEngine;

public static class GameFormulas
{
    // 1. 移动速度公式
    public static float CalcMoveSpeed(float enginePower, float mass, float envSpeedMult)
    {
        float safeMass = Mathf.Max(mass, 0.5f);
        // 👇【核心修复】：给所有机甲 3.0 的基础移速保底！就算没装引擎也不会变成乌龟了！
        float baseSpeed = 1.0f;
        return (baseSpeed + (enginePower / safeMass)) * envSpeedMult;
    }


    // GameFormulas.cs
    public static float CalcStaggerTime(float impulse, float mass)
    {
        float safeMass = Mathf.Max(mass, 0.5f);
        float deltaV = impulse / safeMass;

        // 1. 阈值保护：冲击力太小，大质量单位直接“霸体”免疫硬直
        if (deltaV < 2.0f) return 0f;

        // 2. 软上限控制：哪怕受到核弹冲击，硬直时间也绝不能超过 2.5 秒 (防止玩家被控死)
        float baseStagger = deltaV * 0.05f; // 基础转化率
        float maxStagger = 2.5f;

        // 经典衰减映射
        return maxStagger * (1f - Mathf.Exp(-baseStagger / maxStagger));
    }

    // 假设武器图纸上填的 AttackSpeed 是一个 0~100+ 的“加速评分”
    // 基础冷却我们假定为 2.0 秒（或者你可以从武器SO里读一个 BaseCooldown）
    // 极限最快射速限制为 0.2 秒
    public static float CalcCooldown(float attackSpeedScore)
    {
        float baseCooldown = 2.0f;
        float minCooldown = 0.2f;

        if (attackSpeedScore <= 0f) return baseCooldown;

        // 使用经典的衰减公式： y = Min + (Base - Min) * (100 / (100 + X))
        // 当 score = 0 时，冷却 = 2.0s
        // 当 score = 100 时，冷却 = 0.2 + 1.8 * 0.5 = 1.1s
        // 当 score = 300 时，冷却 = 0.2 + 1.8 * 0.25 = 0.65s
        // 永远平滑，永远递减，永远达不到 0.2s！
        float factor = 100f / (100f + attackSpeedScore);

        return minCooldown + (baseCooldown - minCooldown) * factor;
    }

    public static void CalcDamageReduction(float rawDamage, float currentAP, bool isTrueDamage, out float finalHPDamage, out float armorAbsorbed)
    {
        if (isTrueDamage || currentAP <= 0)
        {
            armorAbsorbed = 0f;
            finalHPDamage = rawDamage;
            return;
        }

        if (rawDamage <= currentAP)
        {
            armorAbsorbed = rawDamage;
            finalHPDamage = 0f;
        }
        else
        {
            armorAbsorbed = currentAP;
            finalHPDamage = rawDamage - currentAP;
        }
    }
}