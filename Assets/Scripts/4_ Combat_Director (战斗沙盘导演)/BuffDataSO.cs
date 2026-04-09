using System.Collections.Generic;
using UnityEngine;

// 维度一：生命周期控制
public enum BuffDurationType
{
    Permanent,      // 永久 (直到被驱散)
    Refreshable,    // 时限：再次获得时，重置持续时间
    Blocking        // 阻塞：再次获得时，无视新时间，原时间继续倒数
}

// 维度二：堆叠方式控制
public enum BuffStackType
{
    NonStackable,   // 不可叠层 (Max = 1)
    LinearScaling,  // 线性叠层 (每层叠加属性/伤害)
    ThresholdTrigger// 阈值触发 (叠满 N 层瞬间引爆，然后清零/移除)
}

[CreateAssetMenu(fileName = "NewBuff", menuName = "Chimera Protocol/1. 核心图纸库/战斗状态 (Buff & Debuff)")]
public class BuffDataSO : ScriptableObject
{
    [Header("=== 基础识别 ===")]
    public string BuffID = "BUFF_001"; // 同源判定依据
    public string BuffName = "新状态";
    public Sprite BuffIcon;
    public Color ParticleColor = Color.white; // 预留给未来特效变色

    [Header("=== 生命周期与堆叠规则 ===")]
    public BuffDurationType DurationType = BuffDurationType.Refreshable;
    public float BaseDuration = 5f; // 持续秒数 (Permanent 填啥都没用)

    public BuffStackType StackType = BuffStackType.LinearScaling;
    public int MaxStacks = 1; // ThresholdTrigger 模式下，这是引爆的层数

    [Header("=== 属性修饰 (随层数放大) ===")]
    [Tooltip("每 1 层 Buff 提供的属性增减 (负数代表 Debuff)")]
    public List<StatEntry> StatModifiers = new List<StatEntry>();

    [Header("=== ECA 生命周期触发器 ===")]
    [Tooltip("挂载瞬间触发 (如：瞬间掉血)")]
    public List<ECAAction> OnApplyActions = new List<ECAAction>();

    [Tooltip("每隔几秒触发一次 OnTickActions？(默认 1.0 秒)")]
    public float TickInterval = 1.0f; // 👇【新增】：暴露触发频率！

    [Tooltip("每隔一定时间触发一次 (如：毒药持续掉血)")]
    public List<ECAAction> OnTickActions = new List<ECAAction>();

    [Tooltip("自然结束或被驱散时触发 (如：定时炸弹)")]
    public List<ECAAction> OnRemoveActions = new List<ECAAction>();

    [Tooltip("仅 ThresholdTrigger 有效：叠满 N 层时瞬间触发 (如：射钉枪爆甲)")]
    public List<ECAAction> OnMaxStacksActions = new List<ECAAction>();
}