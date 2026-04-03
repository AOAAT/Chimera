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

    // 1. 底盘池
    private readonly StatType[] ChassisPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 2. 核心组件池
    private readonly StatType[] CorePool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 3. 移动组件池
    private readonly StatType[] MovementPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost, StatType.EnginePower
    };

    // 4. 武器组件池
    private readonly StatType[] WeaponPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost,
        StatType.MaxDamage, StatType.MinDamage, StatType.MaxRange, StatType.MinRange,
        StatType.AttackSpeed, StatType.CriticalChance, StatType.ExplosionRadius,
        StatType.MultiShotCount, StatType.ProjectileSpeed
    };

    // 5. 辅助/工厂组件池
    private readonly StatType[] SupportPool = {
        StatType.AddedHP, StatType.AddedAP, StatType.AddedMass, StatType.PowerCost
    };

    // 6. 敌人本体池
    private readonly StatType[] EnemyPool = {
        StatType.HP, StatType.AP, StatType.Mass, StatType.MoveSpeed
    };

    // ==========================================

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 开始绘制属性
        EditorGUI.BeginProperty(position, label, property);

        // 获取内部的两个变量：StatID 和 Value
        SerializedProperty statIdProp = property.FindPropertyRelative("StatID");
        SerializedProperty valueProp = property.FindPropertyRelative("Value");

        // --- 核心魔法 1：感知宿主身份 ---
        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        StatType[] allowedPool = GetAllowedPool(targetObject);

        // --- 核心魔法 2：构建动态下拉菜单 ---
        // 把允许的枚举转换成字符串数组，供下拉菜单显示
        string[] displayOptions = allowedPool.Select(s => s.ToString()).ToArray();

        // 查找当前已经选中的值，在我们的允许池里排第几个？
        StatType currentStat = (StatType)statIdProp.intValue;
        int currentIndex = Array.IndexOf(allowedPool, currentStat);

        // 防呆：如果当前值被剔除了（比如你把武器改成了核心），强制变成池子里的第一个，并标红警告！
        bool isInvalid = false;
        if (currentIndex == -1)
        {
            currentIndex = 0;
            isInvalid = true;
        }

        // --- 排版计算 (极其舒爽的左右分栏比例 6:4) ---
        Rect dropdownRect = new Rect(position.x, position.y, position.width * 0.6f - 5f, position.height);
        Rect valueRect = new Rect(position.x + position.width * 0.6f, position.y, position.width * 0.4f, position.height);

        // --- 绘制下拉菜单 ---
        if (isInvalid) GUI.backgroundColor = Color.red; // 脏数据标红！

        int newIndex = EditorGUI.Popup(dropdownRect, currentIndex, displayOptions);

        GUI.backgroundColor = Color.white; // 恢复颜色

        // 保存玩家的选择
        if (newIndex >= 0 && newIndex < allowedPool.Length)
        {
            statIdProp.intValue = (int)allowedPool[newIndex];
        }

        // --- 绘制数值输入框 ---
        EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    // 智能大脑：判断当前在谁的肚子里？
    private StatType[] GetAllowedPool(UnityEngine.Object targetObject)
    {
        // 宿主是底盘图纸？
        if (targetObject is ChassisDataSO) return ChassisPool;

        // 宿主是敌人图纸？
        if (targetObject is EnemyDataSO) return EnemyPool;

        // 宿主是机甲组件图纸？
        if (targetObject is ComponentDataSO comp)
        {
            switch (comp.Type)
            {
                case ComponentType.Core: return CorePool;
                case ComponentType.Weapon: return WeaponPool;
                case ComponentType.Movement: return MovementPool;
                case ComponentType.Support:
                case ComponentType.Factory: return SupportPool;
            }
        }

        // 宿主是 ECA 万能修饰器？(Action_ModifyComponentStat)
        // 这种情况下，修饰器可能要修改任何属性，所以开放所有权限！
        return (StatType[])Enum.GetValues(typeof(StatType));
    }
}
#endif