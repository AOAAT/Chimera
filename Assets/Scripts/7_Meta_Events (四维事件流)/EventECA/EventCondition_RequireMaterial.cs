using UnityEngine;

[CreateAssetMenu(fileName = "Cond_RequireMaterial", menuName = "Chimera Protocol/Event ECA/Condition: Require Material (需要废料)")]
public class EventCondition_RequireMaterial : EventCondition
{
    public int RequiredAmount = 50;

    public override bool Evaluate(out string failReason)
    {
        if (GlobalResourceManager.Instance.Materials >= RequiredAmount)
        {
            failReason = "";
            return true;
        }
        failReason = $"需要 {RequiredAmount} 废料 (当前: {GlobalResourceManager.Instance.Materials})";
        return false;
    }
}