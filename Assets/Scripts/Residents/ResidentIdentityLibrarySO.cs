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

    [Header("=== 随机居民特性池 ===")]
    public List<ResidentTraitDefinitionSO> TraitPool = new List<ResidentTraitDefinitionSO>();
    [Min(0)] public int MinRandomTraits = 1;
    [Min(0)] public int MaxRandomTraits = 1;

    public ResidentData GenerateRandom()
    {
        string randomName = NamePool.Count > 0 ? NamePool[Random.Range(0, NamePool.Count)] : "无名居民";
        ResidentData resident = new ResidentData(randomName)
        {
            TechProficiency = Random.Range(0.8f, 1.2f),
            FleshProficiency = Random.Range(0.8f, 1.2f),
            ManaProficiency = Random.Range(0.8f, 1.2f),
            Discipline = Random.Range(0.2f, 0.9f),
            Sociability = Random.Range(0.2f, 0.9f),
            Courage = Random.Range(0.2f, 0.9f)
        };

        List<ResidentTraitDefinitionSO> available = TraitPool.FindAll(x => x != null && !string.IsNullOrWhiteSpace(x.TraitID));
        int minimum = Mathf.Clamp(MinRandomTraits, 0, available.Count);
        int maximum = Mathf.Clamp(MaxRandomTraits, minimum, available.Count);
        int traitCount = available.Count > 0 ? Random.Range(minimum, maximum + 1) : 0;
        for (int i = 0; i < traitCount; i++)
        {
            int index = Random.Range(0, available.Count);
            resident.TraitIDs.Add(available[index].TraitID);
            available.RemoveAt(index);
        }
        return resident;
    }

    public ResidentTraitDefinitionSO GetTrait(string traitID)
    {
        if (string.IsNullOrWhiteSpace(traitID)) return null;
        return TraitPool.Find(x => x != null && x.TraitID == traitID);
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
