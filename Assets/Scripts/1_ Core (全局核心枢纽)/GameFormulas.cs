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
    // GameFormulas.cs
    // GameFormulas.cs
    public static float CalcCooldown(float attackSpeedScore)
    {
        float baseCooldown = 2.0f;
        float minCooldown = 0.2f;

        // 👇【架构师级修改：动态锚点解算】
        // 策划需求：评分达到 100 时，冷却必须精准等于 1.0 秒
        float targetScore = 100f;
        float targetCooldown = 1.0f;

        if (attackSpeedScore <= 0f) return baseCooldown;

        // 运行时根据策划填的锚点，利用自然对数 (Mathf.Log) 逆向推导出完美的衰减常数
        // 这样代码就彻底变成了“数据驱动”，策划怎么改锚点都不会崩
        float ratio = (targetCooldown - minCooldown) / (baseCooldown - minCooldown);
        float decayConstant = -targetScore / Mathf.Log(ratio); // 对于(100, 1.0)，这里算出来大概是 123.315

        float factor = Mathf.Exp(-attackSpeedScore / decayConstant);

        return minCooldown + (baseCooldown - minCooldown) * factor;
    }

    public static void CalcDamageReduction(float rawDamage, float currentAP, float currentBlock, bool isTrueDamage, out float finalHPDamage, out float armorAbsorbed)
    {
        // 1. 如果是真实伤害，无视格挡和AP，直接打肉！
        if (isTrueDamage)
        {
            armorAbsorbed = 0f;
            finalHPDamage = rawDamage;
            return;
        }

        // 2. 普通伤害：先经过格挡 (Block) 的削减
        float damageAfterBlock = Mathf.Max(0f, rawDamage - currentBlock);

        // 如果连格挡都没打穿，直接刮痧，AP和HP都不掉
        if (damageAfterBlock <= 0f)
        {
            armorAbsorbed = 0f;
            finalHPDamage = 0f;
            return;
        }

        // 3. 再打护甲血条 (AP)
        if (currentAP <= 0)
        {
            armorAbsorbed = 0f;
            finalHPDamage = damageAfterBlock;
            return;
        }

        if (damageAfterBlock <= currentAP)
        {
            armorAbsorbed = damageAfterBlock;
            finalHPDamage = 0f;
        }
        else
        {
            armorAbsorbed = currentAP;
            finalHPDamage = damageAfterBlock - currentAP;
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