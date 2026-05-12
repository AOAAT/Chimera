// --- Action_HolyHornAoE.cs (V2.0 加固版) ---
using UnityEngine;          // 👈 解决 Vector3 不存在的报错
using System.Collections.Generic;
using System.Linq;           // 👈 解决 List 过滤和 Count() 逻辑

[CreateAssetMenu(fileName = "HolyHornAoE_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 圣音号角 V2")]
public class Action_HolyHornAoE : ECAAction
{
    [Header("=== 范围配置 ===")]
    public float MaxRadius = 5.0f;

    // 构造函数设定优先级：属于投递层 (Priority 200)
    public Action_HolyHornAoE()
    {
        Priority = 200;
    }

    public override void Execute(ECAContext context)
    {
        // 核心加固：如果没有来源实体或武器，直接熔断
        if (context.SourceEntity == null || context.SourceWeapon == null) return;

        // 1. 计算适配沙盒的真实半径
        float realRadius = CombatSandbox.GetDist(MaxRadius);

        // 2. 搜寻范围内的存活敌人 (使用 Vector3.Distance)
        // 注意：这里需要确保 CombatDirector.ActiveEnemies 是 List 类型
        var targets = CombatDirector.ActiveEnemies
            .Where(e => e != null && e.CurrentHP > 0 && Vector3.Distance(context.SourceEntity.position, e.transform.position) <= realRadius)
            .ToList();

        // 3. 判定目标是否存在
        // 👈 修复 CS0019：确保 Count 是属性或 Count() 是方法
        if (targets == null || targets.Count == 0) return;

        // 4. 【ECA 2.0 核心】：分发命中管线
        // 我们不再这里直接扣血，而是让每个人都走一遍该武器的 OnHit 流程（从而兼容附魔）
        foreach (var t in targets)
        {
            // 呼叫 RuntimeWeapon 中新写的 TriggerHitPipeline
            context.SourceWeapon.TriggerHitPipeline(t.transform, t.transform.position, context);
        }

        // 5. 【ECA 2.0 核心】：标记发射权已被接管
        // 告诉 WeaponModule：我已经通过 AOE 方式处理了攻击，你不用再发那一颗默认子弹了
        context.IsHandledByCustomDelivery = true;

        Debug.Log($"<color=#FFD700>【圣音分发】</color> 成功对 {targets.Count} 个目标分发了命中管线。");
    }
}