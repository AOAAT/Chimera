using UnityEngine;

[CreateAssetMenu(fileName = "Act_GrantProtocol", menuName = "Chimera Protocol/3. 宏观控制/事件 ECA - 签署协议")]
public class EventAction_GrantProtocol : EventAction
{
    public BuffDataSO ProtocolBuff;
    public int DurationBattles = 1;

    public override void Execute()
    {
        if (ProtocolBuff != null && GlobalProtocolRegistry.Instance != null)
        {
            GlobalProtocolRegistry.Instance.AddProtocol(ProtocolBuff, DurationBattles);
        }
    }
}