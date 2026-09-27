// --- 修改后的 Action_MirrorLastSkill.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "MirrorLastSkill", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 镜像复刻")]
public class Action_MirrorLastSkill : ECAAction
{
    public GameObject FailVFX;

    public override void Execute(ECAContext context)
    {
        // 🌟【核心修复 A】：递归守卫
        // 如果当前上下文中已经标记了“正在镜像中”，则禁止再次进入镜像逻辑
        if (context.CustomStates.ContainsKey("INTERNAL_MIRROR_LOCK"))
        {
            Debug.LogWarning("<color=red>【防熔断系统】检测到潜在递归，已自动拦截镜像套娃。</color>");
            return;
        }

        // 1. 检查是否有记忆
        if (SkillCastHistory.Instance == null || SkillCastHistory.Instance.MemorizedSkill == null)
        {
            if (FailVFX != null) Instantiate(FailVFX, context.SourceEntity.position, Quaternion.identity);
            context.ExecutionAborted = true;
            return;
        }

        // 2. 提取记忆
        ActiveSkillConfig targetSkill = SkillCastHistory.Instance.MemorizedSkill;

        // 🌟【核心修复 B】：双重身份检查（防止字符串由于空格导致的判定失败）
        if (targetSkill.SkillName.Contains("缸中") || targetSkill.OnSkillCastActions.Contains(this))
        {
            Debug.LogError("【逻辑错误】尝试复刻镜像技能本身，操作已阻止。");
            return;
        }

        // 3. 执行镜像
        Debug.Log($"<color=#FF00FF>【镜像执行】</color> 正在释放复刻招式：{targetSkill.SkillName}");

        // 在执行前，给 Context 打上锁定标记
        context.CustomStates["INTERNAL_MIRROR_LOCK"] = 1.0f;

        foreach (var action in targetSkill.OnSkillCastActions)
        {
            if (action != null)
            {
                action.Execute(context);
            }
        }

        // 执行完后，移除标记（维持 Context 链条干净）
        context.CustomStates.Remove("INTERNAL_MIRROR_LOCK");
    }
}