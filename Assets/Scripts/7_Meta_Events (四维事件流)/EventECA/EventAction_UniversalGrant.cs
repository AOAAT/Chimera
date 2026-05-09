using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// --- 👇【关键修复】：补回丢失的枚举定义 ---
public enum RewardType
{
    SpecificComponent,
    RandomLootBox,
    GlobalProtocol
}

[CreateAssetMenu(fileName = "Act_MultiGrant", menuName = "Chimera Protocol/Event ECA/万能奖励发放 (多选版)")]
public class EventAction_UniversalGrant : EventAction
{
    // --- 定义单个奖励项的结构 ---
    [System.Serializable]
    public class GrantEntry
    {
        public RewardType Mode = RewardType.SpecificComponent;

        [Header("特定组件/底盘")]
        public ComponentDataSO ComponentBlueprint;
        public int Level = 1;

        [Header("随机盲盒序列")]
        public LootSequenceSO LootPool;

        [Header("全局协议 (直接生效，不进大巴扎)")]
        public BuffDataSO ProtocolBuff;
        public int ProtocolDuration = 1;
    }

    [Header("=== 奖励清单 (可添加多项) ===")]
    public List<GrantEntry> Rewards = new List<GrantEntry>();

    public override void Execute()
    {
        if (Rewards == null || Rewards.Count == 0) return;

        // 我们将所有需要“去大巴扎挑选”的任务汇总到一个列表中
        List<ActiveLootTask> tasksForBazaar = new List<ActiveLootTask>();

        foreach (var entry in Rewards)
        {
            switch (entry.Mode)
            {
                case RewardType.SpecificComponent:
                    if (entry.ComponentBlueprint != null)
                    {
                        // 包装成一个“确定内容”的盲盒任务
                        tasksForBazaar.Add(CreateFixedTask(entry.ComponentBlueprint, entry.Level));
                    }
                    break;

                case RewardType.RandomLootBox:
                    if (entry.LootPool != null)
                    {
                        // 将随机序列中的所有任务加入清单
                        foreach (var tConfig in entry.LootPool.Tasks)
                        {
                            tasksForBazaar.Add(new ActiveLootTask { Config = tConfig });
                        }
                    }
                    break;

                case RewardType.GlobalProtocol:
                    if (entry.ProtocolBuff != null)
                    {
                        // 全局协议直接注册，不进大巴扎 UI
                        if (GlobalProtocolRegistry.Instance != null)
                            GlobalProtocolRegistry.Instance.AddProtocol(entry.ProtocolBuff, entry.ProtocolDuration);
                    }
                    break;
            }
        }

        // --- 启动大巴扎 ---
        if (tasksForBazaar.Count > 0)
        {
            if (LootUIManager.Instance != null)
            {
                LootUIManager.Instance.OpenHub(tasksForBazaar, () => HandlePostLootFlow());
            }
            else
            {
                Debug.LogError("【系统错误】找不到 LootUIManager 实例，无法发放奖励！");
                HandlePostLootFlow(); // 强制尝试继续剧情，防止卡死
            }
        }
        else
        {
            // 如果清单里全是全局协议（没进大巴扎），则直接判定后续流程
            HandlePostLootFlow();
        }
    }

    // 辅助方法：把特定组件包装成一个“已开封”的打捞任务
    private ActiveLootTask CreateFixedTask(ComponentDataSO bp, int lv)
    {
        LootTaskConfig mockConfig = new LootTaskConfig { Mode = LootDropMode.CustomPoolDrop };
        return new ActiveLootTask
        {
            Config = mockConfig,
            IsBoxOpened = true, // 标记为已开启
            GeneratedItems = new List<InstancedComponent> { new InstancedComponent(bp, lv) }
        };
    }

    private void HandlePostLootFlow()
    {
        if (EventDirector.Instance == null) return;

        // 从导演那里拿回刚才寄存的“下一幕”
        EventNodeSO next = EventDirector.GetPendingNextNode();
        EventDirector.ClearPendingNextNode(); // 阅后即焚

        if (next != null)
        {
            Debug.Log($"<color=#00FFFF>【剧情接力】</color> 奖励发放完毕，正在唤醒：{next.EventTitle}");
            EventDirector.Instance.PlayEvent(next);
        }
        else
        {
            Debug.Log("<color=white>【剧情终结】</color> 流程结束，返回大地图。");
            EventDirector.Instance.ExecuteReturnToMap();
        }
    }
}