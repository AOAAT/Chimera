using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ==========================================
// 奖励类型枚举
// ==========================================
public enum RewardType
{
    SpecificComponent, // 特定组件/底盘
    RandomLootBox,     // 随机盲盒序列
    GlobalProtocol     // 全局协议 (直接生效)
}

[CreateAssetMenu(fileName = "Act_MultiGrant", menuName = "Chimera Protocol/Event ECA/万能奖励发放 (多选+强制锁)")]
public class EventAction_UniversalGrant : EventAction
{
    // ==========================================
    // 单项奖励配置结构
    // ==========================================
    [System.Serializable]
    public class GrantEntry
    {
        public RewardType Mode = RewardType.SpecificComponent;

        [Tooltip("如果勾选，玩家在大巴扎中只能点击‘领取’，不能‘粉碎’（用于教程防死档）")]
        public bool ForceClaim = false;

        [Header("模式：特定组件/底盘")]
        public ComponentDataSO ComponentBlueprint;
        public int Level = 1;

        [Header("模式：随机盲盒序列")]
        public LootSequenceSO LootPool;

        [Header("模式：全局协议")]
        public BuffDataSO ProtocolBuff;
        public int ProtocolDuration = 1;
    }

    [Header("=== 奖励清单 (可添加多项) ===")]
    public List<GrantEntry> Rewards = new List<GrantEntry>();

    // ==========================================
    // 执行主逻辑
    // ==========================================
    public override void Execute()
    {
        if (Rewards == null || Rewards.Count == 0) return;

        // 收集所有需要进入大巴扎展示的任务
        List<ActiveLootTask> tasksForBazaar = new List<ActiveLootTask>();

        foreach (var entry in Rewards)
        {
            switch (entry.Mode)
            {
                case RewardType.SpecificComponent:
                    if (entry.ComponentBlueprint != null)
                    {
                        var task = CreateFixedTask(entry.ComponentBlueprint, entry.Level);
                        // --- 👇 注入强制领取标志 ---
                        task.IsForceClaim = entry.ForceClaim;
                        tasksForBazaar.Add(task);
                    }
                    break;

                case RewardType.RandomLootBox:
                    if (entry.LootPool != null)
                    {
                        foreach (var tConfig in entry.LootPool.Tasks)
                        {
                            var task = new ActiveLootTask { Config = tConfig };
                            // --- 👇 盲盒也可以设置整体强制领取 ---
                            task.IsForceClaim = entry.ForceClaim;
                            tasksForBazaar.Add(task);
                        }
                    }
                    break;

                case RewardType.GlobalProtocol:
                    if (entry.ProtocolBuff != null && GlobalProtocolRegistry.Instance != null)
                    {
                        // 协议类直接在后台静默录入
                        GlobalProtocolRegistry.Instance.AddProtocol(entry.ProtocolBuff, entry.ProtocolDuration);
                    }
                    break;
            }
        }

        // --- 呼叫 UI 链路 ---
        if (tasksForBazaar.Count > 0)
        {
            if (LootUIManager.Instance != null)
            {
                // 开启大巴扎并注入“流程接力”回调
                LootUIManager.Instance.OpenHub(tasksForBazaar, () => HandlePostLootFlow());
            }
            else
            {
                Debug.LogError("【系统错误】找不到 LootUIManager，跳过奖励展示直接推进剧情。");
                HandlePostLootFlow();
            }
        }
        else
        {
            // 如果清单里只有直接生效的协议，则直接判定下一步剧情
            HandlePostLootFlow();
        }
    }

    // ==========================================
    // 辅助与回调逻辑
    // ==========================================

    private ActiveLootTask CreateFixedTask(ComponentDataSO bp, int lv)
    {
        // 伪造一个已打开的盲盒任务
        LootTaskConfig mockConfig = new LootTaskConfig { Mode = LootDropMode.CustomPoolDrop };
        return new ActiveLootTask
        {
            Config = mockConfig,
            IsBoxOpened = true,
            GeneratedItems = new List<InstancedComponent> { new InstancedComponent(bp, lv) }
        };
    }

    private void HandlePostLootFlow()
    {
        if (EventDirector.Instance == null) return;

        // 从事件导演那里回收刚才寄存的“接力信封”
        EventNodeSO next = EventDirector.GetPendingNextNode();
        EventDirector.ClearPendingNextNode(); // 阅后即焚

        if (next != null)
        {
            Debug.Log($"<color=#00FFFF>【剧情接力】</color> 奖励发放闭环，开启下一幕：{next.EventTitle}");
            EventDirector.Instance.PlayEvent(next);
        }
        else
        {
            Debug.Log("<color=white>【剧情终结】</color> 任务链结束，执行返图协议。");
            EventDirector.Instance.ExecuteReturnToMap();
        }
    }
}