using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResidentIdentityLibrary", menuName = "Chimera Protocol/居民系统/身份配置库")]
public class ResidentIdentityLibrarySO : ScriptableObject
{
    [Header("=== 全局属性配置 ===")]
    public float DefaultResidentHP = 20f;

    [Header("=== 随机姓名池 ===")]
    public List<string> NamePool = new List<string> { "凯恩", "艾莉丝", "莫顿", "维嘉", "希尔" };

    [Header("=== 彩蛋人物预设 (接口预留) ===")]
    public List<HeroResidentConfig> HeroPresets = new List<HeroResidentConfig>();

    public ResidentData GenerateRandom()
    {
        string randomName = NamePool[Random.Range(0, NamePool.Count)];
        return new ResidentData(randomName);
    }

    // 未来在这里实现根据 ID 获取特定英雄的逻辑
}

[System.Serializable]
public class HeroResidentConfig
{
    public string HeroID;
    public string HeroName;
    [TextArea] public string Lore;
    // 预留特质初始化配置
}