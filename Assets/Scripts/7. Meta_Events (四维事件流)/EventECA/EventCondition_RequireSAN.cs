using UnityEngine;

[CreateAssetMenu(fileName = "Cond_RequireSAN", menuName = "Chimera Protocol/Event ECA/Condition: Require SAN (需要理智)")]
public class EventCondition_RequireSAN : EventCondition
{
    public int RequiredAmount = 30;

    public override bool Evaluate(out string failReason)
    {
        if (GlobalResourceManager.Instance.CurrentSAN >= RequiredAmount)
        {
            failReason = "";
            return true;
        }
        failReason = $"需要 {RequiredAmount} SAN值 (当前: {GlobalResourceManager.Instance.CurrentSAN})";
        return false;
    }
}