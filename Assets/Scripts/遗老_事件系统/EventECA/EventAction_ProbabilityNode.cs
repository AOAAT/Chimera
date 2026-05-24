using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Act_ProbCheck", menuName = "Chimera Protocol/Event ECA/双向概率跳转")]
public class EventAction_ProbabilityNode : EventAction
{
    [Range(0f, 1f)] public float SuccessChance = 0.5f;

    [Header("=== 成功分支 ===")]
    public List<EventAction> OnSuccess;
    public EventNodeSO NextSuccessEvent;

    [Header("=== 失败分支 ===")]
    public List<EventAction> OnFail;
    public EventNodeSO NextFailEvent;

    public override void Execute()
    {
        if (Random.value <= SuccessChance)
        {
            Debug.Log("<color=green>【判定成功】</color>");
            if (OnSuccess != null) foreach (var a in OnSuccess) a.Execute();
            if (NextSuccessEvent != null) EventDirector.Instance.PlayEvent(NextSuccessEvent);
        }
        else
        {
            Debug.Log("<color=red>【判定失败】</color>");
            if (OnFail != null) foreach (var a in OnFail) a.Execute();
            // 👇 现在失败也会跳转到指定的反馈页面
            if (NextFailEvent != null) EventDirector.Instance.PlayEvent(NextFailEvent);
        }
    }
}