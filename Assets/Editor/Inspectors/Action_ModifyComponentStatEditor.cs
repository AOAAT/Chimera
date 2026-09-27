// --- Action_ModifyComponentStatEditor.cs ---
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Action_ModifyComponentStat))]
public class Action_ModifyComponentStatEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.BeginVertical("box");
        GUI.color = new Color(0.8f, 0.8f, 1f); // 给优先级框上个淡蓝色
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Priority"), new GUIContent("⚡ 执行优先级 (Priority)"));
        GUI.color = Color.white;
        EditorGUILayout.HelpBox("小值先行。例：闸门 0-99，修正 100-199，发射 200。", MessageType.None);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        // --- 2. 👇【核心新增】：配件契约折叠区 ---
        RenderAccessoryContract();

        EditorGUILayout.Space();


        // 1. 绘制目标过滤
        EditorGUILayout.LabelField("修改谁？", EditorStyles.boldLabel);
        SerializedProperty filterProp = serializedObject.FindProperty("Filter");
        EditorGUILayout.PropertyField(filterProp);
        TargetFilterType currentFilter = (TargetFilterType)filterProp.enumValueIndex;
        if (currentFilter == TargetFilterType.ByTag) EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetTag"));
        else if (currentFilter == TargetFilterType.ByType) EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetType"));
        else if (currentFilter == TargetFilterType.ByMacro) EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetMacro"));

        EditorGUILayout.Space();

        // 2. 绘制数值操作
        EditorGUILayout.LabelField("修改什么？", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetStat"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Operation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Value"), new GUIContent("基础值 (Base Value)"));

        EditorGUILayout.Space();

        // 3. 绘制动态系数（核心魔法区）
        EditorGUILayout.LabelField("动态系数来源", EditorStyles.boldLabel);
        SerializedProperty scaleModeProp = serializedObject.FindProperty("ScaleBy");
        EditorGUILayout.PropertyField(scaleModeProp);

        if ((ScalingMode)scaleModeProp.enumValueIndex == ScalingMode.ComponentCount)
        {
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ScalingMacro"), new GUIContent("统计该阵营数量"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExcludeSourceFromCount"), new GUIContent("排除触发者零件"));

            // 👇【核心新增】：暴露底盘计数开关
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeChassisInCount"), new GUIContent("统计底盘本身"));

            EditorGUILayout.HelpBox("最终加成 = 基础值 × (匹配零件数 + 底盘判定)", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
        else if ((ScalingMode)scaleModeProp.enumValueIndex == ScalingMode.EmptySocketCount)
        {
            // --- 👇【核心新增】：空槽位模式下的提示 ---
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.HelpBox("【空槽位鉴定模式】\n最终加成 = 基础值 × 机甲剩余未安装零件的插槽数量。\n适合设计‘孤狼’、‘轻量化’或‘断舍离’流派的组件。", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void RenderAccessoryContract()
    {
        SerializedProperty isAccProp = serializedObject.FindProperty("IsAccessory");

        EditorGUILayout.BeginVertical("helpbox");

        // 核心勾选框
        isAccProp.boolValue = EditorGUILayout.ToggleLeft(" 🛠️ 作为附魔配件使用 (开启注入契约)", isAccProp.boolValue, EditorStyles.boldLabel);

        if (isAccProp.boolValue)
        {
            EditorGUILayout.Space(5);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("注入准入条件：", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("AllowedComponentType"), new GUIContent("限定组件大类"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RequiredDelivery"), new GUIContent("限定投递方式"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RequiredTags"), new GUIContent("必需标签 (Any)"), true);

            EditorGUILayout.HelpBox("只有完全满足上述条件的零件插槽，才能装配此积木。", MessageType.Info);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }
}

#endif