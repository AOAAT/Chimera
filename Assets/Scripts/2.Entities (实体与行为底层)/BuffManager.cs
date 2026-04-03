using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(DamageReceiver))]
public class BuffManager : MonoBehaviour
{
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private DamageReceiver myReceiver;

    // 缓存由 Buff 提供的动态属性增益（每帧重算）
    public Dictionary<StatType, float> BuffStatModifiers = new Dictionary<StatType, float>();

    private void Awake() { myReceiver = GetComponent<DamageReceiver>(); }

    // ==========================================
    // 核心接口：外部(ECA)给实体挂载 Buff
    // ==========================================
    public void ApplyBuff(BuffDataSO buffData, ECAContext sourceContext)
    {
        if (buffData == null || myReceiver == null || myReceiver.CurrentHP <= 0) return;

        ActiveBuff existingBuff = activeBuffs.Find(b => b.Blueprint.BuffID == buffData.BuffID);

        // 1. 如果身上没有这个 Buff，直接新建并挂载！
        if (existingBuff == null)
        {
            ActiveBuff newBuff = new ActiveBuff(buffData);
            activeBuffs.Add(newBuff);
            ExecuteActions(buffData.OnApplyActions, sourceContext);
            RecalculateModifiers();
            return;
        }

        // 2. 如果身上已经有了，处理【堆叠规则】(Stack)
        if (buffData.StackType != BuffStackType.NonStackable)
        {
            if (existingBuff.CurrentStacks < buffData.MaxStacks)
            {
                existingBuff.CurrentStacks++;
                RecalculateModifiers();
            }

            // 【阈值引爆判定】：如果叠满了！
            if (buffData.StackType == BuffStackType.ThresholdTrigger && existingBuff.CurrentStacks >= buffData.MaxStacks)
            {
                Debug.Log($"<color=#FF8C00>【Buff 引爆】</color> {gameObject.name} 身上的 [{buffData.BuffName}] 叠满了！");
                ExecuteActions(buffData.OnMaxStacksActions, sourceContext);

                // 引爆后彻底清除该 Buff
                activeBuffs.Remove(existingBuff);
                RecalculateModifiers();
                return; // 引爆了就不用走下面的刷新时间逻辑了
            }
        }

        // 3. 处理【生命周期规则】(Duration)
        if (buffData.DurationType == BuffDurationType.Refreshable)
        {
            existingBuff.RemainingTime = buffData.BaseDuration; // 重置时间
        }
        else if (buffData.DurationType == BuffDurationType.Blocking)
        {
            // 什么都不做，原时间继续倒数
        }
    }

    private void Update()
    {
        if (activeBuffs.Count == 0 || myReceiver.CurrentHP <= 0) return;

        bool needsRecalc = false;

        // 倒序遍历，方便在遍历中安全移除过期的 Buff
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = activeBuffs[i];

            // 构造一个简单的上下文，Target 就是自己
            ECAContext context = new ECAContext { PrimaryTarget = this.transform, ImpactPoint = this.transform.position };

            buff.UpdateTimers(context);

            if (buff.Blueprint.DurationType != BuffDurationType.Permanent && buff.RemainingTime <= 0)
            {
                // Buff 自然过期！
                ExecuteActions(buff.Blueprint.OnRemoveActions, context);
                activeBuffs.RemoveAt(i);
                needsRecalc = true;
            }
        }

        if (needsRecalc) RecalculateModifiers();
    }

    // 每当 Buff 增减或层数变化时，重新汇总所有属性修饰值
    private void RecalculateModifiers()
    {
        BuffStatModifiers.Clear();
        foreach (var buff in activeBuffs)
        {
            if (buff.Blueprint.StatModifiers == null) continue;

            // 线性叠加：每层的属性增益 * 当前层数
            int multiplier = buff.Blueprint.StackType == BuffStackType.LinearScaling ? buff.CurrentStacks : 1;

            foreach (var stat in buff.Blueprint.StatModifiers)
            {
                float totalValue = stat.Value * multiplier;
                if (BuffStatModifiers.ContainsKey(stat.StatID)) BuffStatModifiers[stat.StatID] += totalValue;
                else BuffStatModifiers[stat.StatID] = totalValue;
            }
        }
    }

    private void ExecuteActions(List<ECAAction> actions, ECAContext context)
    {
        if (actions == null || actions.Count == 0) return;
        foreach (var action in actions) if (action != null) action.Execute(context);
    }
}