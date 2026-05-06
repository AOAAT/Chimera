using UnityEngine;

[CreateAssetMenu(fileName = "Act_TriggerCombat", menuName = "Chimera Protocol/Event ECA/强行开启战斗")]
public class EventAction_TriggerSpecialCombat : EventAction
{
    public EncounterLayoutSO SpecialLayout;

    // --- EventAction_TriggerSpecialCombat.cs ---

    public override void Execute()
    {
        if (SpecialLayout == null || CombatDirector.Instance == null || EventDirector.Instance == null) return;

        Debug.Log($"<color=red>【特殊开战】</color> 正在将仪式现场载入沙盘，锚定节点: {EventDirector.Instance.CurrentNodeData.NodeID}");

        // 1. 设置布局
        CombatDirector.Instance.CurrentLayout = SpecialLayout;
        MusicManager.Instance?.SwitchState(MusicState.Combat);
        // --- 👇【关键修复】：显式交接节点数据 ---
        // 这样战斗导演就会记住这个节点，打完后能正确结账
        CombatDirector.Instance.EnterCombatPhase(EventDirector.Instance.CurrentNodeData);
        // ------------------------------------------
    }
}