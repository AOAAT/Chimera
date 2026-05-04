// --- EventAction_TriggerSpecialCombat.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "Act_TriggerCombat", menuName = "Chimera Protocol/Event ECA/强行开启战斗")]
public class EventAction_TriggerSpecialCombat : EventAction
{
    public EncounterLayoutSO SpecialLayout;

    public override void Execute()
    {
        if (SpecialLayout == null || CombatDirector.Instance == null) return;

        Debug.Log("<color=red>【战术干预】</color> 事件触发了强制战斗！");

        // 1. 关闭事件面板
        if (EventDirector.Instance != null) EventDirector.Instance.EventPanel.SetActive(false);

        // 2. 注入特殊布局并开启战斗
        CombatDirector.Instance.CurrentLayout = SpecialLayout;
        // 注意：这里需要确保 CombatDirector 已经清理过注册表
        CombatDirector.Instance.EnterCombatPhase(null); // 传入 null 代表它是非地图节点触发的
    }
}