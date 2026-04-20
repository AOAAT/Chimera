using UnityEngine;

[CreateAssetMenu(fileName = "MirrorLastSkill", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 镜像复刻")]
public class Action_MirrorLastSkill : ECAAction
{
    [Header("=== 失败反馈 ===")]
    public GameObject FailVFX; // 如果没记忆，播一个哑火特效

    public override void Execute(ECAContext context)
    {
        // 1. 检查是否有记忆
        if (SkillCastHistory.Instance == null || SkillCastHistory.Instance.MemorizedSkill == null)
        {
            Debug.LogWarning("【缸中之脑】当前无记忆招式，无法释放！");

            // 补偿逻辑：如果复刻失败，返还一半 CP (可选)
            // GlobalCPManager.Instance.ModifyCP(context.ChassisData.CoreActiveSkill.CPCost * 0.5f);

            if (FailVFX != null) Instantiate(FailVFX, context.SourceEntity.position, Quaternion.identity);

            // 熔断后续逻辑
            context.ExecutionAborted = true;
            return;
        }

        // 2. 提取记忆
        ActiveSkillConfig targetSkill = SkillCastHistory.Instance.MemorizedSkill;
        Debug.Log($"<color=#FF00FF>【镜像执行】</color> 缸中之脑正在释放：{targetSkill.SkillName}");

        // 3. 递归执行记忆技能中的所有积木
        foreach (var action in targetSkill.OnSkillCastActions)
        {
            if (action != null)
            {
                action.Execute(context);
            }
        }
    }
}