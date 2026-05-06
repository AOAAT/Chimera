#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 告诉 Unity，只要在面板上画 StatEntry 这个结构体，就统统交给我来管！
[CustomPropertyDrawer(typeof(StatEntry))]
public class StatEntryDrawer : PropertyDrawer
{
    // ==========================================
    // 🎨 主策的专属词条池 (你可以随时在这里增删改！)
    // ==========================================

    // ==========================================
    // 🎨 主策的专属词条池
    // ==========================================

    // 1. 底盘池 (加入了 AddedBlock)
    private readonly StatType[] ChassisPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 2. 核心组件池
    private readonly StatType[] CorePool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 3. 移动组件池
    private readonly StatType[] MovementPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 4. 武器组件池 (武器一般不加格挡，保持原样)
    private readonly StatType[] WeaponPool = {
    StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost,
    StatType.MaxDamage, StatType.MinDamage, StatType.MaxRange, StatType.MinRange,
    StatType.AttackSpeed, StatType.CriticalChance,
    StatType.CritMultiplier, // 👈 【核心新增】：让武器图纸的下拉菜单能选到它
    StatType.ExplosionRadius, StatType.MultiShotCount, StatType.ProjectileSpeed
};
    // 5. 辅助/工厂组件池 (加入了 AddedBlock，极品防御插件的来源！)
    private readonly StatType[] SupportPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost
    };

    // 6. 敌人本体池 (加入了怪物的专属 Block)
    private readonly StatType[] EnemyPool = {
        StatType.HP, StatType.AP, StatType.Block, StatType.Mass, StatType.MoveSpeed
    };
    // ==========================================

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 🌟【核心修复点】：编辑器安全检查
        // 如果序列化对象已经被销毁或为空，立即停止绘制，防止触发 Unity 内部异常
        if (property == null || property.serializedObject == null || property.serializedObject.targetObject == null)
        {
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty statIdProp = property.FindPropertyRelative("StatID");
        SerializedProperty valueProp = property.FindPropertyRelative("Value");

        // 再次检查内部属性
        if (statIdProp == null || valueProp == null)
        {
            EditorGUI.EndProperty();
            return;
        }

        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        StatType[] allowedPool = GetAllowedPool(targetObject);

        string[] displayOptions = allowedPool.Select(s => s.ToString()).ToArray();
        StatType currentStat = (StatType)statIdProp.intValue;
        int currentIndex = Array.IndexOf(allowedPool, currentStat);

        bool isInvalid = false;
        if (currentIndex == -1)
        {
            currentIndex = 0;
            isInvalid = true;
        }

        Rect dropdownRect = new Rect(position.x, position.y, position.width * 0.6f - 5f, position.height);
        Rect valueRect = new Rect(position.x + position.width * 0.6f, position.y, position.width * 0.4f, position.height);

        if (isInvalid) GUI.backgroundColor = Color.red;

        int newIndex = EditorGUI.Popup(dropdownRect, currentIndex, displayOptions);

        GUI.backgroundColor = Color.white;

        if (newIndex >= 0 && newIndex < allowedPool.Length)
        {
            statIdProp.intValue = (int)allowedPool[newIndex];
        }

        EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    // 智能大脑：判断当前在谁的肚子里？
    private StatType[] GetAllowedPool(UnityEngine.Object targetObject)
    {
        if (targetObject is ChassisDataSO) return new StatType[] { StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower };
        if (targetObject is EnemyDataSO) return new StatType[] { StatType.HP, StatType.AP, StatType.Block, StatType.Mass, StatType.MoveSpeed };
        if (targetObject is ComponentDataSO comp)
        {
            switch (comp.Type)
            {
                case ComponentType.Core: return new StatType[] { StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower };
                case ComponentType.Weapon: return new StatType[] { StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.MaxDamage, StatType.MinDamage, StatType.MaxRange, StatType.MinRange, StatType.AttackSpeed, StatType.CriticalChance, StatType.CritMultiplier, StatType.ExplosionRadius, StatType.MultiShotCount, StatType.ProjectileSpeed };
                case ComponentType.Movement: return new StatType[] { StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower };
                case ComponentType.Support:
                case ComponentType.Factory: return new StatType[] { StatType.AddedHP, StatType.AddedAP, StatType.AddedBlock, StatType.AddedMass, StatType.PowerCost };
            }
        }
        return (StatType[])Enum.GetValues(typeof(StatType));
    }
}
#endif