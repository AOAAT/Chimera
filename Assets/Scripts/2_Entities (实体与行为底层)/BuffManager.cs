using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(DamageReceiver))]
public class BuffManager : MonoBehaviour
{
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private DamageReceiver myReceiver;

    public Dictionary<StatType, float> BuffStatModifiers = new Dictionary<StatType, float>();
    public Dictionary<StatType, float> AdditiveModifiers = new Dictionary<StatType, float>();
    public Dictionary<StatType, float> MultiplierModifiers = new Dictionary<StatType, float>();
    public bool HasAIOverride { get; private set; }
    public MovementStrategy CurrentOverrideMovement { get; private set; }
    public float CurrentOverrideDodgeDist { get; private set; }

    public event Action OnBuffsChanged;
    public IReadOnlyList<ActiveBuff> GetActiveBuffs() => activeBuffs;

    private void Awake()
    {
        myReceiver = GetComponent<DamageReceiver>();
    }

    public void ApplyBuff(BuffDataSO buffData, ECAContext sourceContext)
    {
        if (buffData == null || myReceiver == null || myReceiver.CurrentHP <= 0) return;

        ActiveBuff existingBuff = activeBuffs.Find(b => b.Blueprint.BuffID == buffData.BuffID);

        if (existingBuff == null)
        {
            ActiveBuff newBuff = new ActiveBuff(buffData);
            activeBuffs.Add(newBuff);

            if (buffData.OnApplyActions != null)
            {
                foreach (var action in buffData.OnApplyActions)
                    if (action != null) action.Execute(sourceContext);
            }
        }
        else
        {
            if (buffData.StackType != BuffStackType.NonStackable)
            {
                if (existingBuff.CurrentStacks < buffData.MaxStacks) existingBuff.CurrentStacks++;
                if (buffData.StackType == BuffStackType.ThresholdTrigger && existingBuff.CurrentStacks >= buffData.MaxStacks)
                {
                    if (buffData.OnMaxStacksActions != null)
                        foreach (var action in buffData.OnMaxStacksActions) if (action != null) action.Execute(sourceContext);
                    activeBuffs.Remove(existingBuff);
                    RecalculateModifiers();
                    return;
                }
            }
            if (buffData.DurationType == BuffDurationType.Refreshable) existingBuff.RemainingTime = buffData.BaseDuration;
        }
        RecalculateModifiers();
    }

    private void Update()
    {
        if (activeBuffs.Count == 0 || myReceiver.CurrentHP <= 0) return;

        bool needsRecalc = false;
        // 【核心修复】：实时获取真实的敌我阵营
        bool currentIsEnemy = myReceiver != null ? myReceiver.isEnemy : false;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = activeBuffs[i];

            // 构造真实的上下文
            ECAContext context = new ECAContext
            {
                SourceEntity = this.transform,
                PrimaryTarget = this.transform,
                ImpactPoint = this.transform.position,
                IsEnemyFire = currentIsEnemy, // 👈 关键：这里不再是 false
                ChassisData = null
            };

            buff.Update(Time.deltaTime, context);

            if (buff.Blueprint.DurationType != BuffDurationType.Permanent)
            {
                buff.RemainingTime -= Time.deltaTime;
                if (buff.RemainingTime <= 0)
                {
                    if (buff.Blueprint.OnRemoveActions != null)
                        foreach (var action in buff.Blueprint.OnRemoveActions) if (action != null) action.Execute(context);
                    activeBuffs.RemoveAt(i);
                    needsRecalc = true;
                }
            }
        }
        if (needsRecalc) RecalculateModifiers();
    }

    private void RecalculateModifiers()
    {
        AdditiveModifiers.Clear();
        MultiplierModifiers.Clear();
        HasAIOverride = false;

        foreach (var buff in activeBuffs)
        {
            if (buff.Blueprint.StatModifiers != null)
            {
                int stackCount = (buff.Blueprint.StackType == BuffStackType.LinearScaling) ? buff.CurrentStacks : 1;

                foreach (var mod in buff.Blueprint.StatModifiers)
                {
                    float totalValue = mod.Value * stackCount;

                    if (mod.ModType == BuffModifierType.Additive)
                    {
                        if (AdditiveModifiers.ContainsKey(mod.StatID)) AdditiveModifiers[mod.StatID] += totalValue;
                        else AdditiveModifiers.Add(mod.StatID, totalValue);
                    }
                    else // Multiplier 模式
                    {
                        // 我们采用加法叠乘 (1 + 0.2 + 0.1 = 1.3倍)，这是最稳健的数值平衡方案
                        if (MultiplierModifiers.ContainsKey(mod.StatID)) MultiplierModifiers[mod.StatID] += totalValue;
                        else MultiplierModifiers.Add(mod.StatID, totalValue);
                    }
                }
            }
            if (buff.Blueprint.OverrideAI)
            {
                HasAIOverride = true;
                CurrentOverrideMovement = buff.Blueprint.OverrideMovementLogic;
                CurrentOverrideDodgeDist = buff.Blueprint.OverrideSafeDodgeDistance;
            }
        }
        OnBuffsChanged?.Invoke();
    }
    public float GetAdjustedStat(StatType type, float baseValue)
    {
        float add = AdditiveModifiers.ContainsKey(type) ? AdditiveModifiers[type] : 0;
        float mul = MultiplierModifiers.ContainsKey(type) ? MultiplierModifiers[type] : 0;

        // 公式：(基础值 + 绝对值和) * (1 + 百分比和)
        return (baseValue + add) * (1f + mul);
    }
    public int GetBuffStacks(string buffID)
    {
        ActiveBuff b = activeBuffs.Find(x => x.Blueprint.BuffID == buffID);
        return b != null ? b.CurrentStacks : 0;
    }

    public void TriggerHolderDeathActions(ECAContext deathContext)
    {
        foreach (var buff in activeBuffs)
        {
            if (buff.Blueprint.OnHolderDeathActions != null)
                foreach (var action in buff.Blueprint.OnHolderDeathActions) if (action != null) action.Execute(deathContext);
        }
    }
}

[Serializable]
public class ActiveBuff
{
    public BuffDataSO Blueprint;
    public float RemainingTime;
    public float TickTimer;
    public int CurrentStacks;

    public ActiveBuff(BuffDataSO blueprint)
    {
        this.Blueprint = blueprint;
        this.RemainingTime = blueprint.BaseDuration;
        this.CurrentStacks = 1;
        this.TickTimer = 0f;
    }

    public void Update(float deltaTime, ECAContext context)
    {
        if (Blueprint.OnTickActions != null && Blueprint.OnTickActions.Count > 0)
        {
            TickTimer += deltaTime;
            if (TickTimer >= Blueprint.TickInterval)
            {
                TickTimer = 0f;
                foreach (var action in Blueprint.OnTickActions) if (action != null) action.Execute(context);
            }
        }
    }
}