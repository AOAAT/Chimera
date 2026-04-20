using UnityEngine;

[CreateAssetMenu(fileName = "CopyLastSkill", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 复刻上个技能")]
public class Action_CopyLastUsedSkill : ECAAction
{
    public override void Execute(ECAContext context)
    {
        if (SkillCastHistory.Instance == null || SkillCastHistory.Instance.LastUsedSkill == null)
        {
            Debug.LogWarning("暂无历史技能可供复刻");
            return;
        }

        var lastSkill = SkillCastHistory.Instance.LastUsedSkill;
        Debug.Log($"【缸中之脑】正在解析并释放历史技能: {lastSkill.SkillName}");

        // 递归执行历史技能的所有动作
        foreach (var action in lastSkill.OnSkillCastActions)
        {
            action.Execute(context);
        }
    }
}