using UnityEngine;
using System.Collections.Generic; // 必须加这个

public class ECAContext
{
    public Vector3 ImpactPoint;
    public Transform PrimaryTarget;
    public float BaseDamage;
    public RuntimeWeapon SourceWeapon;
    public bool IsCriticalHit;
    public RuntimeChimeraData ChassisData;
    public ComponentDataSO SourceComponentSO;
    public bool IsEnemyFire;
    public Transform SourceEntity;

    public bool ExecutionAborted = false;

    // 👇【核心补全】：万能自定义状态字典，用于积木间通讯
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
}

public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}