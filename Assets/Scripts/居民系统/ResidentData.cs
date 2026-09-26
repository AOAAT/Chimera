using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResidentStatus { Idle, Working, Piloting, TravelingToWork }

[Serializable]
public class ResidentData
{
    [Header("=== 基础身份 ===")]
    public string InstanceID;
    public string ResidentName;
    public bool IsHero = false; // 是否为彩蛋/英雄单位

    [Header("=== 职业等级 (接口预留) ===")]
    public int Level = 1;
    public float Experience = 0f;

    [Header("=== 特质系统 (接口预留) ===")]
    // 未来在这里挂载具体的特质 ScriptableObject 或 ID
    public List<string> TraitIDs = new List<string>();

    [Header("=== 熟练度权重 (系别加成) ===")]
    public float TechProficiency = 1.0f;
    public float FleshProficiency = 1.0f;
    public float ManaProficiency = 1.0f;

    [Header("=== 性格倾向 (0-1) ===")]
    [Range(0f, 1f)] public float Discipline = 0.5f;
    [Range(0f, 1f)] public float Sociability = 0.5f;
    [Range(0f, 1f)] public float Courage = 0.5f;


    public ResidentData(string name, bool isHero = false)
    {
        InstanceID = Guid.NewGuid().ToString();
        ResidentName = name;
        this.IsHero = isHero;

    }
    public ResidentStatus Status = ResidentStatus.Idle;
    public string CurrentCarrierID; // 记录当前所在的建筑或机甲 InstanceID
    public float CurrentHP;
    // 预留：经验增加接口
    public void AddExperience(float amount)
    {
        Experience += amount;
        // 这里未来编写升级逻辑
    }
    public bool CanGoOffDuty() => Status == ResidentStatus.Working || Status == ResidentStatus.Piloting;
}
