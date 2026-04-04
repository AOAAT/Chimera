// --- START OF FILE Action_PlayVFX.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "PlayVFX", menuName = "Chimera Protocol/ECA Actions/Feedback: Play VFX (播放粒子特效)")]
public class Action_PlayVFX : ECAAction
{
    [Header("=== 特效配置 ===")]
    [Tooltip("拖入做好的粒子系统预制体")]
    public GameObject VFXPrefab;

    [Tooltip("如果勾选，特效会跟随目标移动 (如持续燃烧)；不勾选则留在原地 (如爆血、枪口焰)")]
    public bool AttachToTarget = false;

    [Tooltip("特效播放完后多久销毁？(秒)")]
    public float AutoDestroyTime = 2f;

    public override void Execute(ECAContext context)
    {
        if (VFXPrefab == null) return;

        // 1. 生成特效 (ImpactPoint 已经是极其精准的枪口/受击点)
        GameObject vfxInstance = Instantiate(VFXPrefab, context.ImpactPoint, Quaternion.identity);

        // 2. 判定是否需要挂载在敌人身上
        if (AttachToTarget && context.PrimaryTarget != null)
        {
            vfxInstance.transform.SetParent(context.PrimaryTarget);
        }

        // 3. 自动销毁回收
        Destroy(vfxInstance, AutoDestroyTime);
    }
}