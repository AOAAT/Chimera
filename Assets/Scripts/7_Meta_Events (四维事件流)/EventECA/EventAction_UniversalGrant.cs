using UnityEngine;

// 确保枚举在命名空间可见范围内
public enum RewardType { SpecificComponent, RandomLootBox, GlobalProtocol }

[CreateAssetMenu(fileName = "Act_UniversalGrant", menuName = "Chimera Protocol/Event ECA/万能奖励发放")]
public class EventAction_UniversalGrant : EventAction
{
    public RewardType Mode = RewardType.RandomLootBox;

    [Header("模式：特定组件")]
    public ComponentDataSO ComponentBlueprint;
    public int Level = 1;

    [Header("模式：随机盲盒")]
    public LootSequenceSO LootPool;

    [Header("模式：全局协议")]
    public BuffDataSO ProtocolBuff;
    public int ProtocolDuration = 1;

    public override void Execute()
    {
        switch (Mode)
        {
            case RewardType.SpecificComponent:
                if (ComponentBlueprint != null)
                    PlayerInventoryManager.Instance.AddComponentToInventory(ComponentBlueprint, Level);
                break;

            case RewardType.RandomLootBox:
                if (LootPool != null)
                    LootSequenceDirector.Instance.StartLootHub(LootPool, null, MacroCategory.Tech, MapManager.Instance.CurrentLayer, () => {
                        // 大巴扎结束后，手动通知事件导演返回地图
                        if (EventDirector.Instance != null) EventDirector.Instance.ExecuteReturnToMap();
                    });
                break;

            case RewardType.GlobalProtocol:
                if (ProtocolBuff != null && GlobalProtocolRegistry.Instance != null)
                    GlobalProtocolRegistry.Instance.AddProtocol(ProtocolBuff, ProtocolDuration);
                break;
        }
    }
}