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

    // 👇【核心新增】：记录这发攻击是敌方发起的，还是玩家发起的！
    public bool IsEnemyFire;
}

public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}