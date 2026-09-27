using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "AreaApplyMultiBuff", menuName = "Chimera Protocol/2. ECA 机制积木/状态 - 落点范围施加多层Buff")]
public class Action_AreaApplyMultiBuff : ECAAction
{
    public BuffDataSO BuffToApply;
    public float Radius = 4f;
    [Tooltip("一次性施加的层数")]
    public int Stacks = 3;

    public override void Execute(ECAContext context)
    {
        if (BuffToApply == null) return;

        float realRadius = CombatSandbox.GetDist(Radius);
        // 1. 索敌 (仅限敌方)
        var targets = CombatDirector.ActiveEnemies.Where(e =>
            e != null && e.CurrentHP > 0 &&
            Vector3.Distance(context.ImpactPoint, e.transform.position) <= realRadius);

        foreach (var t in targets)
        {
            BuffManager mgr = t.GetComponent<BuffManager>();
            if (mgr != null)
            {
                // 2. 循环施加指定层数
                for (int i = 0; i < Stacks; i++)
                {
                    mgr.ApplyBuff(BuffToApply, context);
                }
            }
        }
    }
}