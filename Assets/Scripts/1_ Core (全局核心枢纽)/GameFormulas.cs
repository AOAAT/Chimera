using UnityEngine;

// ==========================================
// 全局战斗与物理公式核心库 (主策调参专用)
// ==========================================
public static class GameFormulas
{
    //[Header("=== 机动与物理引擎 ===")]

    // 1. 移动速度公式
    // 逻辑：引擎总出力 ÷ 质量 = 基础速度。再乘以全局环境系数。
    public static float CalcMoveSpeed(float enginePower, float mass, float envSpeedMult)
    {
        float safeMass = Mathf.Max(mass, 0.5f); // 兜底：防止质量为0导致无穷大
        return Mathf.Max(0.1f, (enginePower / safeMass) * envSpeedMult);
    }

    // 2. 体力上限公式
    // 逻辑：引擎总出力 与 耗电量 的比值，决定了机甲的“肺活量”。
    public static float CalcMaxStamina(float enginePower, float totalPowerCost)
    {
        float safePowerCost = Mathf.Max(totalPowerCost, 1f);
        return 100f * (enginePower / safePowerCost); // 基础体力为 100
    }

    // 3. 物理冲击与硬直时间公式 (Stagger)
    // 逻辑：冲量 ÷ 质量 = 速度变化量(DeltaV)。DeltaV 越大，硬直时间越长。
    public static float CalcStaggerTime(float impulse, float mass)
    {
        float safeMass = Mathf.Max(mass, 0.5f);
        float deltaV = impulse / safeMass;

        if (deltaV < 0.5f) return 0f; // 冲击力太小，免疫硬直

        // 核心系数：1点速度变化量 = 0.05秒硬直
        return Mathf.Max(0.1f, deltaV * 0.05f);
    }

    //[Header("=== 战斗数值解算 ===")]

    // 4. 攻速转冷却时间公式 (AttackSpeed to Cooldown)
    // 逻辑：经典公式 100 / 攻速。例如 50攻速 = 2秒，100攻速 = 1秒，200攻速 = 0.5秒
    public static float CalcCooldown(float attackSpeed)
    {
        if (attackSpeed <= 0f) return 999f;
        return 100f / attackSpeed;
    }

    // 5. 护甲与真实伤害结算公式
    // 逻辑：分离原始伤害，返回被护甲吸收的量以及打在肉体上的真实量
    public static void CalcDamageReduction(float rawDamage, float currentAP, bool isTrueDamage, out float finalHPDamage, out float armorAbsorbed)
    {
        // 真实伤害直接跳过护甲
        if (isTrueDamage || currentAP <= 0)
        {
            armorAbsorbed = 0f;
            finalHPDamage = rawDamage;
            return;
        }

        // 普通伤害：优先消耗 AP
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