// --- 请完全替换 EventAction_GrantLoot.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "Act_GrantLoot", menuName = "Chimera Protocol/3. 宏观控制/事件 ECA - 发放奖励")]
public class EventAction_GrantLoot : EventAction
{
    public LootSequenceSO RewardLoot;

    public override void Execute()
    {
        if (RewardLoot != null)
        {
            Debug.Log("【事件触发】遭遇奇遇，开启专属大巴扎打捞！");

            // 👇【核心修复】：传入委托，打捞结束后，让事件导演去通知大地图结算！
            LootSequenceDirector.Instance.StartLootHub(RewardLoot, null, MacroCategory.Tech, MapManager.Instance.CurrentLayer, () =>
            {
                if (EventDirector.Instance != null) EventDirector.Instance.ExecuteReturnToMap();
            });
        }
    }
}