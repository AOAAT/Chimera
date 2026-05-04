// --- EventAction_ProbabilityNode.cs ---
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Act_ProbCheck", menuName = "Chimera Protocol/Event ECA/多重概率判定")]
public class EventAction_ProbabilityNode : EventAction
{
    [Range(0f, 1f)] public float SuccessChance = 0.5f;

    [Header("成功后的动作(给钱/给零件)")]
    public List<EventAction> OnSuccess;

    [Header("成功后跳转的下一个事件(厄舍府-深处)")]
    public EventNodeSO NextSuccessEvent;

    [Header("失败后的后果(扣SAN/降速)")]
    public List<EventAction> OnFail;

    public override void Execute()
    {
        if (Random.value <= SuccessChance)
        {
            foreach (var a in OnSuccess) a.Execute();
            if (NextSuccessEvent != null) EventDirector.Instance.PlayEvent(NextSuccessEvent);
        }
        else
        {
            foreach (var a in OnFail) a.Execute();
            // 失败不跳转，依然停留在当前厄舍府界面，让玩家决定是否继续
            EventDirector.Instance.RefreshCurrentOptions();
        }
    }
}