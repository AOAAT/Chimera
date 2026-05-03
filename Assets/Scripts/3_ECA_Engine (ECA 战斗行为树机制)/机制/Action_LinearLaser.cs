using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Act_LinearLaser", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 发射线性激光")]
public class Action_LinearLaser : ECAAction
{
    public LinearLaserConfig Config;
    public GameObject LaserPrefab;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || LaserPrefab == null) return;

        // 1. 获取引导时长 (攻速决定)
        float totalTime = 1.0f;
        if (context.SourceWeapon != null)
            totalTime = GameFormulas.CalcCooldown(context.SourceWeapon.GetStat(StatType.AttackSpeed));

        // 2. 生成激光
        GameObject laserObj = Instantiate(LaserPrefab, context.SourceEntity.position, Quaternion.identity);
        // 让激光跟随发射者移动
        laserObj.transform.SetParent(context.SourceEntity);

        LinearLaserController ctrl = laserObj.GetComponent<LinearLaserController>();

        if (ctrl != null)
        {
            // --- 👇【核心对齐】：四个参数 ---
            // 1. Context
            // 2. 积木上的 Config
            // 3. 来源武器的命中积木列表
            // 4. 计算好的总时长
            List<ECAAction> hitActions = context.SourceWeapon != null ? context.SourceWeapon.OnHitActions : new List<ECAAction>();
            ctrl.Initialize(context, Config, hitActions, totalTime);
        }

        // 拦截原本可能的默认判定
        context.ExecutionAborted = true;
    }
}