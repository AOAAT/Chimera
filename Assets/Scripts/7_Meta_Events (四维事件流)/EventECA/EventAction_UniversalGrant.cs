using UnityEngine;

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
                {
                    // 生成一个带星级的零件实例，但不直接存入背包
                    InstancedComponent newItem = new InstancedComponent(ComponentBlueprint, Level);

                    // --- 👇【关键重构：仪式感展示】 ---
                    if (LootSequenceDirector.Instance != null)
                    {
                        LootSequenceDirector.Instance.StartImmediateLoot(newItem, () => {
                            // 展示界面关闭后，通知事件导演回地图
                            if (EventDirector.Instance != null) EventDirector.Instance.ExecuteReturnToMap();
                        });
                    }
                }
                break;

            case RewardType.RandomLootBox:
                if (LootPool != null)
                {
                    LootSequenceDirector.Instance.StartLootHub(LootPool, null, MacroCategory.Tech, 1, () => {
                        if (EventDirector.Instance != null) EventDirector.Instance.ExecuteReturnToMap();
                    });
                }
                break;

            case RewardType.GlobalProtocol:
                if (ProtocolBuff != null)
                    GlobalProtocolRegistry.Instance.AddProtocol(ProtocolBuff, ProtocolDuration);
                break;
        }
    }
}