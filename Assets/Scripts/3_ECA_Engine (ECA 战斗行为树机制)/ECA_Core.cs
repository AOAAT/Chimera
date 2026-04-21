using UnityEngine;
using System.Collections.Generic;

public class ECAContext
{
    // --- 基础物理与身份数据 ---
    public Vector3 ImpactPoint;
    public Transform PrimaryTarget;
    public float BaseDamage;
    public RuntimeWeapon SourceWeapon;
    public bool IsCriticalHit;
    public RuntimeChimeraData ChassisData;
    public ComponentDataSO SourceComponentSO;
    public bool IsEnemyFire;
    public Transform SourceEntity;

    // --- 逻辑控制开关 ---
    public bool ExecutionAborted = false; // 熔断开关

    // --- 👇【核心新增】：瞬时态演算数据 ---
    // 默认均为 1.0 (代表不增不减)，积木可以动态修改这些倍率
    public float TemporaryCritModifier = 1.0f;
    public float TemporaryDamageModifier = 1.0f;

    // --- 通讯字典 ---
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
}

public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}