// --- START OF FILE BuffManager.cs ---
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageReceiver))]
public class BuffManager : MonoBehaviour
{
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private DamageReceiver myReceiver;
    public Dictionary<StatType, float> BuffStatModifiers = new Dictionary<StatType, float>();

    // 👇 暴露事件和列表
    public event Action OnBuffsChanged;
    public IReadOnlyList<ActiveBuff> GetActiveBuffs() => activeBuffs;

    private void Awake() { myReceiver = GetComponent<DamageReceiver>(); }

    public void ApplyBuff(BuffDataSO buffData, ECAContext sourceContext)
    {
        if (buffData == null || myReceiver == null || myReceiver.CurrentHP <= 0) return;
        ActiveBuff existingBuff = activeBuffs.Find(b => b.Blueprint.BuffID == buffData.BuffID);

        if (existingBuff == null)
        {
            activeBuffs.Add(new ActiveBuff(buffData));
            ExecuteActions(buffData.OnApplyActions, sourceContext);
            RecalculateModifiers();
            return;
        }

        if (buffData.StackType != BuffStackType.NonStackable)
        {
            if (existingBuff.CurrentStacks < buffData.MaxStacks)
            {
                existingBuff.CurrentStacks++;
                RecalculateModifiers();
            }
            if (buffData.StackType == BuffStackType.ThresholdTrigger && existingBuff.CurrentStacks >= buffData.MaxStacks)
            {
                ExecuteActions(buffData.OnMaxStacksActions, sourceContext);
                activeBuffs.Remove(existingBuff);
                RecalculateModifiers();
                return;
            }
        }

        if (buffData.DurationType == BuffDurationType.Refreshable) existingBuff.RemainingTime = buffData.BaseDuration;

        // 刷新 UI
        OnBuffsChanged?.Invoke();
    }

    private void Update()
    {
        if (activeBuffs.Count == 0 || myReceiver.CurrentHP <= 0) return;
        bool needsRecalc = false;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = activeBuffs[i];
            ECAContext context = new ECAContext { PrimaryTarget = this.transform, ImpactPoint = this.transform.position };
            buff.UpdateTimers(context);

            if (buff.Blueprint.DurationType != BuffDurationType.Permanent && buff.RemainingTime <= 0)
            {
                ExecuteActions(buff.Blueprint.OnRemoveActions, context);
                activeBuffs.RemoveAt(i);
                needsRecalc = true;
            }
        }
        if (needsRecalc) RecalculateModifiers();
    }

    private void RecalculateModifiers()
    {
        BuffStatModifiers.Clear();
        foreach (var buff in activeBuffs)
        {
            if (buff.Blueprint.StatModifiers == null) continue;
            int multiplier = buff.Blueprint.StackType == BuffStackType.LinearScaling ? buff.CurrentStacks : 1;
            foreach (var stat in buff.Blueprint.StatModifiers)
            {
                float totalValue = stat.Value * multiplier;
                if (BuffStatModifiers.ContainsKey(stat.StatID)) BuffStatModifiers[stat.StatID] += totalValue;
                else BuffStatModifiers[stat.StatID] = totalValue;
            }
        }
        // 👇 任何变化都要通知 UI
        OnBuffsChanged?.Invoke();
    }

    private void ExecuteActions(List<ECAAction> actions, ECAContext context)
    {
        if (actions == null || actions.Count == 0) return;
        foreach (var action in actions) if (action != null) action.Execute(context);
    }
}