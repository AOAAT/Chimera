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

    // --- EventAction_TriggerSpecialCombat.cs ---

    public override void Execute()
    {
        if (SpecialLayout == null || CombatDirector.Instance == null || EventDirector.Instance == null) return;

        Debug.Log($"<color=red>【剧情开战】</color> 正在注入特定布局：{SpecialLayout.name}");

        // 1. 挂载后续钩子
        CombatDirector.Instance.RegisterPostCombatEvents(NextEventOnVictory, NextEventOnFailure);

        // 2. 👇【核心加固】：先关闭事件面板，再进入战斗
        if (EventDirector.Instance.EventPanel != null)
            EventDirector.Instance.EventPanel.SetActive(false);

        // 3. 正式开战
        CombatDirector.Instance.EnterCombatPhase(EventDirector.Instance.CurrentNodeData, SpecialLayout);
    }
}