using UnityEngine;

[CreateAssetMenu(fileName = "Act_ModifyMaterial", menuName = "Chimera Protocol/Event ECA/Action: Modify Material (增减废料)")]
public class EventAction_ModifyMaterial : EventAction
{
    [Tooltip("正数增加，负数扣除")]
    public int Amount = -50; // 默认作为消耗品扣除

    public override void Execute()
    {
        GlobalResourceManager.Instance.ModifyMaterials(Amount);
    }
}