// --- Action_BoostDamageByPower.cs (真实电网接入版) ---
using UnityEngine;

[CreateAssetMenu(fileName = "BoostDamageByPower", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 电能转化伤害 (Real Power)")]
public class Action_BoostDamageByPower : ECAAction
{
    [Tooltip("每 1 点盈余电量转化为多少点额外伤害")]
    public float PowerToDamageRatio = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (GlobalResourceManager.Instance == null)
        {
            Debug.LogWarning("【系统警告】电网管理器不存在，无法计算盈余加成！");
            return;
        }

        // 1. 获取实时盈余电量
        // 公式：最大产能 - 当前战场上所有已部署机甲消耗的总电量
        int maxCap = GlobalResourceManager.Instance.MaxPowerCapacity;
        int usedPower = GlobalResourceManager.Instance.GetTotalUsedPower();

        // 盈余电量不能为负数（如果超载了，加成为0）
        int surplus = Mathf.Max(0, maxCap - usedPower);

        // 2. 计算最终加成
        float bonusDamage = surplus * PowerToDamageRatio;

        // 3. 注入上下文 (引用传递)
        context.BaseDamage += bonusDamage;

        // 4. 调试反馈
        if (surplus > 0)
        {
            Debug.Log($"<color=#FFFF00>[{context.SourceWeapon.WeaponName}]</color> 汲取电网盈余 {surplus}kw，" +
                      $"加成伤害 +{bonusDamage:F1}。最终底数: {context.BaseDamage}");
        }
    }
}