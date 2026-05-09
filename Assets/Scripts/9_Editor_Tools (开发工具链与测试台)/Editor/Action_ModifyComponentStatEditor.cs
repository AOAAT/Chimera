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

        serializedObject.ApplyModifiedProperties();
    }
}
#endif