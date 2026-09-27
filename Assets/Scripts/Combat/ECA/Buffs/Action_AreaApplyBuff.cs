using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "AreaApplyBuff", menuName = "Chimera Protocol/2. ECA 机制积木/状态 - 中心范围施加Buff")]
public class Action_AreaApplyBuff : ECAAction
{
    public BuffDataSO BuffToApply;
    public float Radius = 5f;

    public override void Execute(ECAContext context)
    {
        if (BuffToApply == null) return;

        float realRadius = CombatSandbox.GetDist(Radius);
        // 找到范围内所有敌方单位
        var targets = CombatDirector.ActiveEnemies.Where(e =>
            e != null && e.CurrentHP > 0 &&
            Vector3.Distance(context.ImpactPoint, e.transform.position) <= realRadius);

        foreach (var t in targets)
        {
            BuffManager mgr = t.GetComponent<BuffManager>();
            if (mgr != null) mgr.ApplyBuff(BuffToApply, context);
        }

        Debug.Log($"<color=#32CD32>【蛇首-远程】</color> 毒雾扩散，波及 {targets.Count()} 个单位");
    }
}