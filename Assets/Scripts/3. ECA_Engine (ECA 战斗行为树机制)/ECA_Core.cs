// --- START OF FILE ECA_Core.cs ---
using UnityEngine;

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

    // 👇【核心新增】：熔断开关！一旦有积木把它设为 true，后续积木立即停止！
    public bool ExecutionAborted = false;
}

public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}