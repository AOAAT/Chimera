using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(DamageReceiver))]
public class BuffManager : MonoBehaviour
{
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private DamageReceiver myReceiver;

    // 存储当前所有 Buff 提供的属性总和
    public Dictionary<StatType, float> BuffStatModifiers = new Dictionary<StatType, float>();

    // AI 覆写状态
    public bool HasAIOverride { get; private set; }
    public MovementStrategy CurrentOverrideMovement { get; private set; }
    public float CurrentOverrideDodgeDist { get; private set; }

    // 事件通知
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
            // --- A. 挂载新 Buff ---
            ActiveBuff newBuff = new ActiveBuff(buffData);
            activeBuffs.Add(newBuff);

            // 立即触发挂载积木
            if (buffData.OnApplyActions != null)
            {
                foreach (var action in buffData.OnApplyActions)
                    if (action != null) action.Execute(sourceContext);
            }

            Debug.Log($"<color=#00FF00>【状态机】</color> {gameObject.name} 获得了新 Buff: {buffData.BuffName}");
        }
        else
        {
            // --- B. 处理叠层与刷新 ---
            if (buffData.StackType != BuffStackType.NonStackable)
            {
                if (existingBuff.CurrentStacks < buffData.MaxStacks)
                {
                    existingBuff.CurrentStacks++;
                }

                // 阈值引爆判定
                if (buffData.StackType == BuffStackType.ThresholdTrigger && existingBuff.CurrentStacks >= buffData.MaxStacks)
                {
                    Debug.Log($"<color=red>【引爆】</color> {buffData.BuffName} 叠满 {buffData.MaxStacks} 层，触发引爆！");
                    if (buffData.OnMaxStacksActions != null)
                    {
                        foreach (var action in buffData.OnMaxStacksActions)
                            if (action != null) action.Execute(sourceContext);
                    }
                    activeBuffs.Remove(existingBuff);
                    RecalculateModifiers();
                    OnBuffsChanged?.Invoke();
                    return;
                }
            }

            // 刷新持续时间
            if (buffData.DurationType == BuffDurationType.Refreshable)
                existingBuff.RemainingTime = buffData.BaseDuration;
        }

        RecalculateModifiers();
    }

    private void Update()
    {
        if (activeBuffs.Count == 0 || myReceiver.CurrentHP <= 0) return;

        bool needsRecalc = false;

        // 倒序遍历，安全移除过期 Buff
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = activeBuffs[i];

            // 1. 构建每帧/每秒 Tick 的上下文
            // 这里的 SourceEntity 赋值是“大象腿”这类被动动起来的关键！
            ECAContext context = new ECAContext
            {
                SourceEntity = this.transform,
                PrimaryTarget = this.transform,
                ImpactPoint = this.transform.position,
                IsEnemyFire = false,
                ChassisData = GetComponent<MechUnit2D>() != null ? null : null // 预留
            };

            // 2. 更新内部计时器 (处理 TickActions)
            buff.Update(Time.deltaTime, context);

            // 3. 处理自然过期
            if (buff.Blueprint.DurationType != BuffDurationType.Permanent)
            {
                buff.RemainingTime -= Time.deltaTime;
                if (buff.RemainingTime <= 0)
                {
                    if (buff.Blueprint.OnRemoveActions != null)
                    {
                        foreach (var action in buff.Blueprint.OnRemoveActions)
                            if (action != null) action.Execute(context);
                    }
                    activeBuffs.RemoveAt(i);
                    needsRecalc = true;
                }
            }
        }

        if (needsRecalc) RecalculateModifiers();
    }

    private void RecalculateModifiers()
    {
        BuffStatModifiers.Clear();
        HasAIOverride = false;

        foreach (var buff in activeBuffs)
        {
            // 属性加成计算
            if (buff.Blueprint.StatModifiers != null)
            {
                int multiplier = (buff.Blueprint.StackType == BuffStackType.LinearScaling) ? buff.CurrentStacks : 1;
                foreach (var mod in buff.Blueprint.StatModifiers)
                {
                    float totalValue = mod.Value * multiplier;
                    if (BuffStatModifiers.ContainsKey(mod.StatID)) BuffStatModifiers[mod.StatID] += totalValue;
                    else BuffStatModifiers.Add(mod.StatID, totalValue);
                }
            }

            // AI 覆写检测
            if (buff.Blueprint.OverrideAI)
            {
                HasAIOverride = true;
                CurrentOverrideMovement = buff.Blueprint.OverrideMovementLogic;
                CurrentOverrideDodgeDist = buff.Blueprint.OverrideSafeDodgeDistance;
            }
        }

        OnBuffsChanged?.Invoke();
    }

    public int GetBuffStacks(string buffID)
    {
        ActiveBuff b = activeBuffs.Find(x => x.Blueprint.BuffID == buffID);
        return b != null ? b.CurrentStacks : 0;
    }

    // --- 在 BuffManager.cs 中增加此方法 ---
    public void TriggerHolderDeathActions(ECAContext deathContext)
    {
        // 遍历当前身上所有的 Buff
        foreach (var buff in activeBuffs)
        {
            if (buff.Blueprint.OnHolderDeathActions != null)
            {
                foreach (var action in buff.Blueprint.OnHolderDeathActions)
                {
                    if (action != null)
                    {
                        // Debug.Log($"<color=white>【临终遗言】</color> 触发 Buff:[{buff.Blueprint.BuffName}] 的死亡动作");
                        action.Execute(deathContext);
                    }
                }
            }
        }
    }
}



// ==========================================
// 运行时 Buff 实例：存放动态时间、层数
// ==========================================
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
        // 只有配了 Tick 积木，才跑 Tick 计时器
        if (Blueprint.OnTickActions != null && Blueprint.OnTickActions.Count > 0)
        {
            TickTimer += deltaTime;
            if (TickTimer >= Blueprint.TickInterval)
            {
                TickTimer = 0f;
                // 👇【核心】：大象腿在这里被发射
                foreach (var action in Blueprint.OnTickActions)
                {
                    if (action != null) action.Execute(context);
                }
            }
        }
    }
}