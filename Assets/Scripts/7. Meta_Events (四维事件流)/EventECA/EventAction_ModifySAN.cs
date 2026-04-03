using UnityEngine;

[CreateAssetMenu(fileName = "Act_ModifySAN", menuName = "Chimera Protocol/Event ECA/Action: Modify SAN (增减理智)")]
public class EventAction_ModifySAN : EventAction
{
    [Tooltip("正数回复，负数扣除")]
    public int Amount = -10;

    public override void Execute()
    {
        GlobalResourceManager.Instance.ModifySAN(Amount);
    }
}