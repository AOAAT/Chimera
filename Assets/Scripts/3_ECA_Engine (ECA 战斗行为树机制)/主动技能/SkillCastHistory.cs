using System.Collections.Generic;
using UnityEngine;

public class SkillCastHistory : MonoBehaviour
{
    public static SkillCastHistory Instance;

    // 记录最近一次成功释放的主动技能配置
    public ActiveSkillConfig LastUsedSkill;

    private void Awake() { Instance = this; }

    public void Record(ActiveSkillConfig config)
    {
        // 排除掉“缸中之脑”自身，防止无限递归
        if (config.SkillName == "缸中之脑") return;
        LastUsedSkill = config;
        Debug.Log($"【系统记录】技能历史更新：{config.SkillName}");
    }
}