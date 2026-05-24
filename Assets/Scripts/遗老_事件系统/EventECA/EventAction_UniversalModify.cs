using UnityEngine;

[CreateAssetMenu(fileName = "Act_UniversalModify", menuName = "Chimera Protocol/Event ECA/万能数值修饰")]
public class EventAction_UniversalModify : EventAction
{
    public EventResourceType TargetResource;
    public float Amount = 10f;

    public override void Execute()
    {
        if (GlobalResourceManager.Instance == null) return;

        switch (TargetResource)
        {
          
            case EventResourceType.MaxCP:
                if (GlobalCPManager.Instance != null) GlobalCPManager.Instance.BonusMaxCP += Amount;
                break;
            case EventResourceType.CurrentCP:
                if (GlobalCPManager.Instance != null) GlobalCPManager.Instance.ModifyCP(Amount);
                break;
        }
        Debug.Log($"<color=yellow>【系统】</color> {TargetResource} 已变动: {Amount}");
    }
}