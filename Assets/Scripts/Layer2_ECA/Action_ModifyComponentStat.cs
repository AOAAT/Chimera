using UnityEngine;
using System.Collections.Generic;

public enum TargetFilterType { Self, ByTag, ByType, All }
public enum StatOperation { Add, Multiply }

[CreateAssetMenu(fileName = "ModifyComponentStat", menuName = "Chimera Protocol/ECA Actions/Modifier: Modify Component Stat (万能属性修饰器)")]
public class Action_ModifyComponentStat : ECAAction
{
    public TargetFilterType Filter;
    public SubTag TargetTag;
    public ComponentType TargetType;

    public StatType TargetStat;

    public StatOperation Operation;
    public float Value;

    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null || context.ChassisData.AllEquippedSOs == null) return;

        foreach (var comp in context.ChassisData.AllEquippedSOs)
        {
            if (comp == null) continue;

            bool isMatch = false;
            if (Filter == TargetFilterType.All) isMatch = true;
            else if (Filter == TargetFilterType.Self && comp == context.SourceComponentSO) isMatch = true;
            else if (Filter == TargetFilterType.ByTag && comp.BaseSubTags != null && comp.BaseSubTags.Contains(TargetTag)) isMatch = true;
            else if (Filter == TargetFilterType.ByType && comp.Type == TargetType) isMatch = true;

            if (isMatch)
            {
                // --- 阶段 B：计算差值 (Delta) ---
                float baseVal = GetBaseStat(comp, TargetStat);
                if (baseVal == 0 && Operation == StatOperation.Multiply) continue;

                float delta = 0;
                if (Operation == StatOperation.Add)
                {
                    delta = Value;
                }
                else if (Operation == StatOperation.Multiply)
                {
                    delta = (baseVal * Value) - baseVal;
                }

                context.ChassisData.ModifyStat(comp, TargetStat, delta);

                Debug.Log($"【万能修饰器生效】触发源: {context.SourceComponentSO.ComponentName} | 目标: {comp.ComponentName} | 属性: {TargetStat} 改变了 {delta}");
            }
        }
    }

    // 👇【核心修复】：属性现在存在 LevelMatrix 中，由于 ECA 积木是在运行时执行的，
    // 这里我们统一读取 Level 1 作为乘法计算的基准值，保证属性放大计算有参照物！
    private float GetBaseStat(ComponentDataSO comp, StatType stat)
    {
        var lv1Data = comp.GetLevelData(1);
        if (lv1Data != null && lv1Data.Stats != null)
        {
            foreach (var s in lv1Data.Stats) if (s.StatID == stat) return s.Value;
        }
        return 0f;
    }
}