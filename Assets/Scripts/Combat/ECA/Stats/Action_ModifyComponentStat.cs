// --- Action_ModifyComponentStat.cs 全量版 ---
using UnityEngine;
using System.Collections.Generic;

public enum TargetFilterType { Self, ByTag, ByType, ByMacro, All }
public enum StatOperation { Add, Multiply, Override }
public enum ScalingMode { Constant, ComponentCount, EmptySocketCount }
[CreateAssetMenu(fileName = "ModifyComponentStat", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 万能属性修饰器")]
public class Action_ModifyComponentStat : ECAAction
{
    [Header("=== 1. 目标过滤 (谁被修改) ===")]
    public TargetFilterType Filter;
    public SubTag TargetTag;
    public ComponentType TargetType;
    public MacroCategory TargetMacro;

    [Header("=== 2. 数值设定 ===")]
    public StatType TargetStat;
    public StatOperation Operation;
    public float Value;

    [Header("=== 3. 动态系数 (可选) ===")]
    public ScalingMode ScaleBy = ScalingMode.Constant;
    public MacroCategory ScalingMacro = MacroCategory.Flesh;
    public bool ExcludeSourceFromCount = true;
    public bool IncludeChassisInCount = true;

    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null || context.ChassisData.AllEquippedSOs == null) return;

        // --- 步骤 A：计算动态系数 ---
        float multiplier = 1f;
        if (ScaleBy == ScalingMode.ComponentCount)
        {
            int count = 0;

            // 1. 统计所有零件
            foreach (var comp in context.ChassisData.AllEquippedSOs)
            {
                if (comp != null && comp.MacroCategory == ScalingMacro) count++;
            }

            // 2. 👇【核心新增】：统计底盘自己
            if (IncludeChassisInCount && context.ChassisData.ActiveChassisSO != null)
            {
                if (context.ChassisData.ActiveChassisSO.MacroCategory == ScalingMacro)
                {
                    count++;
                }
            }

            // 3. 判定是否排除触发源零件
            if (ExcludeSourceFromCount && context.SourceComponentSO != null && context.SourceComponentSO.MacroCategory == ScalingMacro)
            {
                count = Mathf.Max(0, count - 1);
            }
            multiplier = count;
        }
        else if (ScaleBy == ScalingMode.EmptySocketCount)
        {
            // --- 👇【核心新增】：空槽位鉴定逻辑 ---
            // 1. 获取底盘总插槽数
            int totalSockets = context.ChassisData.ActiveChassisSO.Sockets.Count;
            // 2. 获取当前已安装的零件总数 (Assemble 已经帮我们存进了 AllEquippedSOs)
            int occupiedSockets = context.ChassisData.AllEquippedSOs.Count;

            // 3. 计算差值即为空槽位数
            int emptyCount = Mathf.Max(0, totalSockets - occupiedSockets);
            multiplier = emptyCount;
        }
        float calculatedValue = Value * multiplier;
        if (calculatedValue == 0 && Operation != StatOperation.Override) return;

        // --- 步骤 B：扫描目标并执行修改 ---
        foreach (var comp in context.ChassisData.AllEquippedSOs)
        {
            if (comp == null) continue;

            bool isMatch = false;
            if (Filter == TargetFilterType.All) isMatch = true;
            else if (Filter == TargetFilterType.Self && comp == context.SourceComponentSO) isMatch = true;
            else if (Filter == TargetFilterType.ByTag && comp.BaseSubTags.Contains(TargetTag)) isMatch = true;
            else if (Filter == TargetFilterType.ByType && comp.Type == TargetType) isMatch = true;
            else if (Filter == TargetFilterType.ByMacro) isMatch = (comp.MacroCategory == TargetMacro);

            if (isMatch)
            {
                float delta = 0;
                if (Operation == StatOperation.Add) delta = calculatedValue;
                else if (Operation == StatOperation.Multiply)
                {
                    float baseVal = GetBaseStat(comp, TargetStat);
                    delta = (baseVal * calculatedValue) - baseVal;
                }
                else if (Operation == StatOperation.Override)
                {
                    float currentVal = IsWeaponStat(TargetStat)
                        ? (context.ChassisData.EquippedWeapons.Find(w => w.SourceSO == comp)?.GetStat(TargetStat) ?? 0)
                        : context.ChassisData.GetGlobalStat(TargetStat);
                    delta = calculatedValue - currentVal;
                }

                context.ChassisData.ModifyStat(comp, TargetStat, delta);
            }
        }
    }

    private bool IsWeaponStat(StatType type) => type >= (StatType)10 && type <= (StatType)19;
    private float GetBaseStat(ComponentDataSO comp, StatType stat)
    {
        var lvData = comp.GetModelData(1);
        if (lvData != null && lvData.Stats != null)
            foreach (var s in lvData.Stats) if (s.StatID == stat) return s.Value;
        return 0f;
    }
}