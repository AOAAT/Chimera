using UnityEngine;

public class SkillCastHistory : MonoBehaviour
{
    public static SkillCastHistory Instance;

    // 记忆中的技能配置
    public ActiveSkillConfig MemorizedSkill { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 记录一次技能释放
    /// </summary>
    public void Record(ActiveSkillConfig config)
    {
        // 核心锁死：缸中之脑不能复刻自己，防止逻辑死循环
        if (config == null || config.SkillName == "缸中之脑") return;

        MemorizedSkill = config;

        // 可以在这里触发一个全局音效，表示“技能已捕捉”
        Debug.Log($"<color=#00FF00>【脑机捕获】</color> 成功录入招式：{config.SkillName}");
    }

    // 战斗结束清理记忆
    public void Clear() { MemorizedSkill = null; }
}