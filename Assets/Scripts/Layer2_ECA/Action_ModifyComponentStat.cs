using UnityEngine;
using System.Collections.Generic;

public enum TargetFilterType { Self, ByTag, ByType, All }
public enum StatOperation { Add, Multiply }

[CreateAssetMenu(fileName = "ModifyComponentStat", menuName = "Chimera Protocol/ECA Actions/Modifier: Modify Component Stat (万能属性修饰器)")]
public class Action_ModifyComponentStat : ECAAction
{
    // 👇【修复1】：删除了所有的 [Header] 标签，消除“双重表头”现象！
    public TargetFilterType Filter;
    public ComponentTag TargetTag;
    public ComponentType TargetType;

    public StatType TargetStat;

    public StatOperation Operation;
    public float Value;

    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null || context.ChassisData.AllEquippedSOs == null) return;

        foreach (var comp in context.ChassisData.AllEquippedSOs)
        {
            // 👇【修复2：核心防呆】：如果这个插槽是空的（没装零件），或者这个零件恰好没有图纸源，直接跳过！绝不能去摸空气！
            if (comp == null) continue;

            // --- 阶段 A：安检门查验身份 ---
            bool isMatch = false;
            if (Filter == TargetFilterType.All) isMatch = true;
            else if (Filter == TargetFilterType.Self && comp == context.SourceComponentSO) isMatch = true;
            // 👇 进一步防呆：确保 comp.Tags 本身不是 null 再去查 Contains
            else if (Filter == TargetFilterType.ByTag && comp.Tags != null && comp.Tags.Contains(TargetTag)) isMatch = true;
            else if (Filter == TargetFilterType.ByType && comp.Type == TargetType) isMatch = true;

            if (isMatch)
            {
                // --- 阶段 B：计算差值 (Delta) ---
                float baseVal = GetBaseStat(comp, TargetStat);
                if (baseVal == 0 && Operation == StatOperation.Multiply) continue; // 基础为0，乘法没意义

                float delta = 0;
                if (Operation == StatOperation.Add)
                {
                    delta = Value;
                }
                else if (Operation == StatOperation.Multiply)
                {
                    // 比如基数是 100，Value 填 0.85 (减耗15%)，那么 Delta 就是 100 * 0.85 - 100 = -15
                    delta = (baseVal * Value) - baseVal;
                }

                // --- 阶段 C：将差值注入机甲黑盒 ---
                context.ChassisData.ModifyStat(comp, TargetStat, delta);

                Debug.Log($"【万能修饰器生效】触发源: {context.SourceComponentSO.ComponentName} | 目标: {comp.ComponentName} | 属性: {TargetStat} 改变了 {delta}");
            }
        }
    }

    // 辅助方法：去 SO 里翻找原始基础属性
    private float GetBaseStat(ComponentDataSO comp, StatType stat)
    {
        if (comp.BaseStats != null)
        {
            foreach (var s in comp.BaseStats) if (s.StatID == stat) return s.Value;
        }
        return 0f;
    }
}