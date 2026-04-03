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

    public static float CalcMaxStamina(float enginePower, float totalPowerCost)
    {
        float safePowerCost = Mathf.Max(totalPowerCost, 1f);
        return 100f * (enginePower / safePowerCost);
    }

    public static float CalcStaggerTime(float impulse, float mass)
    {
        float safeMass = Mathf.Max(mass, 0.5f);
        float deltaV = impulse / safeMass;
        if (deltaV < 0.5f) return 0f;
        return Mathf.Max(0.1f, deltaV * 0.05f);
    }

    public static float CalcCooldown(float attackSpeed)
    {
        if (attackSpeed <= 0f) return 999f;
        return 100f / attackSpeed;
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