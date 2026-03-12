using UnityEngine;

// 1. 数据总线包：当事件发生时，把战场信息打包传给积木
public class ECAContext
{
    public Vector3 ImpactPoint;       // 命中坐标
    public Transform PrimaryTarget;   // 主目标
    public float BaseDamage;          // 基础伤害
    public RuntimeWeapon SourceWeapon;// 伤害来源（武器数据）
    public bool IsCriticalHit;
    public RuntimeChimeraData ChassisData;    // 这台机甲的运行时总线黑盒
    public ComponentDataSO SourceComponentSO; // 提供这个光环的零件图纸（溯源用）
}

// 2. 动作基类：所有具体机制（扣血、爆炸、拉人）的祖宗
// 继承 ScriptableObject，意味着你可以把它当成资产文件存在硬盘里！
public abstract class ECAAction : ScriptableObject
{
    // 强制所有子类必须实现这个执行函数
    public abstract void Execute(ECAContext context);
}