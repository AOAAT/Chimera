using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BranchByDistance", menuName = "Chimera Protocol/2. ECA 机制积木/逻辑 - 距离分支")]
public class Action_BranchByDistance : ECAAction
{
    public float ThresholdDistance = 4.0f;
    [Header("=== 近距离动作 ( < 阈值 ) ===")]
    public List<ECAAction> NearActions;
    [Header("=== 远距离动作 ( > 阈值 ) ===")]
    public List<ECAAction> FarActions;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null || context.SourceEntity == null) return;

        float dist = Vector3.Distance(context.SourceEntity.position, context.PrimaryTarget.position);

        float realThreshold = CombatSandbox.GetDist(ThresholdDistance); // 👈 关键对齐

        if (dist < realThreshold)
        {
            foreach (var a in NearActions) a.Execute(context);
        }
        else
        {
            foreach (var a in FarActions) a.Execute(context);
        }
    }
}