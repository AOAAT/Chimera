using UnityEngine;

[CreateAssetMenu(fileName = "Act_GrantLoot", menuName = "Chimera Protocol/Event ECA/Action: Grant Loot (发放战利品)")]
public class EventAction_GrantLoot : EventAction
{
    [Tooltip("直接调用大巴扎掉落表发放奖励！")]
    public LootSequenceSO RewardLoot;

    public override void Execute()
    {
        if (RewardLoot != null)
        {
            Debug.Log("【事件触发】遭遇奇遇，开启专属大巴扎打捞！");
            // 呼叫战利品导演，发完奖励后它会自动回大地图！
            LootSequenceDirector.Instance.StartLootHub(RewardLoot, null, MacroCategory.Tech, MapManager.Instance.CurrentLayer);
        }
        else
        {
            Debug.LogWarning("【事件警告】未配置事件奖励战利品表！");
        }
    }
}