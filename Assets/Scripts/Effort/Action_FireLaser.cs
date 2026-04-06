// --- START OF FILE Action_FireLaser.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "FireLaser", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 发射激光束 (Fire Laser)")]
public class Action_FireLaser : ECAAction
{
    [Header("=== 激光配置 ===")]
    [Tooltip("拖入挂载了 LaserBeam 脚本和 LineRenderer 的预制体")]
    public GameObject LaserPrefab;

    [Tooltip("激光在画面上残留的时间 (秒)")]
    public float Duration = 0.2f;

    public override void Execute(ECAContext context)
    {
        if (LaserPrefab == null || context.PrimaryTarget == null) return;

        // 1. 生成激光实体
        GameObject laserObj = Instantiate(LaserPrefab, context.ImpactPoint, Quaternion.identity);
        LaserBeam laserScript = laserObj.GetComponent<LaserBeam>();

        if (laserScript != null)
        {
            // 2. 寻找敌人的受击中心 (防止激光射到脚底)
            Vector3 targetPos = context.PrimaryTarget.position;

            // 如果目标有 Collider，尽量射向它的中心
            Collider2D col = context.PrimaryTarget.GetComponentInChildren<Collider2D>();
            if (col != null) targetPos = col.bounds.center;

            // 3. 呼叫激光连线：从枪口 (ImpactPoint) 射向敌人中心 (targetPos)
            laserScript.Fire(context.ImpactPoint, targetPos, Duration);
        }
    }
}