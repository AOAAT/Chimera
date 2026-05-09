using UnityEngine;

[CreateAssetMenu(fileName = "Act_TriggerCombat", menuName = "Chimera Protocol/Event ECA/强行开启战斗")]
public class EventAction_TriggerSpecialCombat : EventAction
{
    public EncounterLayoutSO SpecialLayout;

    [Header("=== 战斗后续跳转 (可选) ===")]
    [Tooltip("战斗获胜并领完奖后，要跳转的事件。如果为空则回大地图")]
    public EventNodeSO NextEventOnVictory;

    [Tooltip("战斗失败（没死的情况下）要跳转的事件")]
    public EventNodeSO NextEventOnFailure;

    public override void Execute()
    {
        if (SpecialLayout == null || CombatDirector.Instance == null || EventDirector.Instance == null) return;

        Debug.Log($"<color=red>【剧情开战】</color> 注入战场并挂载后续剧情钩子。");

        // --- 👇【核心重构】：将后续跳转指令寄存到导演那里 ---
        CombatDirector.Instance.RegisterPostCombatEvents(NextEventOnVictory, NextEventOnFailure);

        // 开启战斗，传入特定布局
        CombatDirector.Instance.EnterCombatPhase(EventDirector.Instance.CurrentNodeData, SpecialLayout);
    }
}