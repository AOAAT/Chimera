using UnityEngine;
using System;

public class SkillCastHistory : MonoBehaviour
{
    public static SkillCastHistory Instance;

    public ActiveSkillConfig MemorizedSkill { get; private set; }

    // 招式更新广播
    public event Action<ActiveSkillConfig> OnMemoryChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 如果是跨关卡游戏，建议加上这句，但目前 V1.0 在战斗场景内即可
            // DontDestroyOnLoad(gameObject); 
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void Record(ActiveSkillConfig config)
    {
        // 核心锁死：缸中之脑不能复刻自己
        if (config == null || config.SkillName == "缸中之脑") return;

        MemorizedSkill = config;

        // 广播：新招式已录入！
        OnMemoryChanged?.Invoke(config);

        Debug.Log($"<color=#00FF00>【记忆录入成功】</color> 招式：{config.SkillName}");
    }

    public void Clear()
    {
        MemorizedSkill = null;
        OnMemoryChanged?.Invoke(null);
    }
}