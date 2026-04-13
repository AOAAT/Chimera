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

    // ==========================================
    // 👇【核心新增】：大运流！真实物理动能碰撞伤害公式
    // ==========================================
    /// <summary>
    /// 计算两个刚体碰撞时产生的动能破坏力 (基于相对速度和双方质量)
    /// </summary>
    /// <param name="massA">主动撞击方质量</param>
    /// <param name="massB">被撞方质量</param>
    /// <param name="relativeVelocity">双方碰撞瞬间的相对速度大小 (m/s)</param>
    /// <param name="damageConversionRate">动能转化为 HP 伤害的比率 (策划用于调控数值膨胀，推荐 1.0~5.0)</param>
    /// <returns>返回这次碰撞产生的总基础伤害</returns>
    public static float CalcKineticRamDamage(float massA, float massB, float relativeVelocity, float damageConversionRate = 2.0f)
    {
        // 防呆：质量不能为 0，否则除以 0 报错
        float m1 = Mathf.Max(0.1f, massA);
        float m2 = Mathf.Max(0.1f, massB);

        // 真实物理学：完全非弹性碰撞的动能损耗公式 (Reduced Mass * V_rel^2 / 2)
        // 折合质量 (Reduced Mass) = (m1 * m2) / (m1 + m2)
        float reducedMass = (m1 * m2) / (m1 + m2);

        // 动能损耗 E = 0.5 * reducedMass * V_rel^2
        float kineticEnergyLoss = 0.5f * reducedMass * (relativeVelocity * relativeVelocity);

        // 转化为游戏里的扣血量 (乘以策划配置的转化率)
        float rawDamage = kineticEnergyLoss * damageConversionRate;

        // 兜底：撞击太轻（比如走路擦到）不造成伤害
        if (rawDamage < 5f) return 0f;

        return rawDamage;
    }
}