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
        if (config == null) return;

        // 🌟【核心修复 C】：深度扫描。如果这个技能的动作列表里含有 Mirror 类型的积木，严禁录入！
        bool containsMirrorAction = false;
        foreach (var action in config.OnSkillCastActions)
        {
            if (action is Action_MirrorLastSkill)
            {
                containsMirrorAction = true;
                break;
            }
        }

        if (containsMirrorAction || config.SkillName.Contains("缸中"))
        {
            // Debug.Log("【审计】跳过镜像类技能的录入。");
            return;
        }

        MemorizedSkill = config;
        OnMemoryChanged?.Invoke(config);
    }

    public void Clear()
    {
        MemorizedSkill = null;
        OnMemoryChanged?.Invoke(null);
    }
}