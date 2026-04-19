using UnityEngine;

[CreateAssetMenu(fileName = "EnergyPulse", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 能量脉冲")]
public class Action_EnergyPulse : ECAAction
{
    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 尝试获取战斗单位身上的视觉管理器
        var visuals = context.SourceEntity.GetComponent<MechEnergyVisuals>();
        if (visuals != null)
        {
            // 触发全机电力闪烁（过载感）
            visuals.PulseAll();
        }
    }
}