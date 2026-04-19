using UnityEngine;

[CreateAssetMenu(fileName = "EnergyLink", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 能量链路 (Energy Link)")]
public class Action_EnergyLink : ECAAction
{
    public GameObject LinePrefab; // 拖入带 LineRenderer 的预制体
    public float Duration = 0.3f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || context.PrimaryTarget == null) return;

        GameObject lineObj = Instantiate(LinePrefab);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();

        if (lr != null)
        {
            // 在机甲核心和目标/武器之间画一道电弧
            lr.SetPosition(0, context.SourceEntity.position);
            lr.SetPosition(1, context.ImpactPoint);
            Destroy(lineObj, Duration);
        }
    }
}