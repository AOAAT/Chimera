#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 告诉 Unity：这段代码是专门用来画 Action_ModifyComponentStat 面板的！
[CustomEditor(typeof(Action_ModifyComponentStat))]
public class Action_ModifyComponentStatEditor : Editor
{
    private SerializedProperty filterProp;
    private SerializedProperty targetTagProp;
    private SerializedProperty targetTypeProp;
    private SerializedProperty targetStatProp;
    private SerializedProperty operationProp;
    private SerializedProperty valueProp;

    private void OnEnable()
    {
        // 抓取脚本里所有的变量名
        filterProp = serializedObject.FindProperty("Filter");
        targetTagProp = serializedObject.FindProperty("TargetTag");
        targetTypeProp = serializedObject.FindProperty("TargetType");
        targetStatProp = serializedObject.FindProperty("TargetStat");
        operationProp = serializedObject.FindProperty("Operation");
        valueProp = serializedObject.FindProperty("Value");
    }

    public override void OnInspectorGUI()
    {
        // 开始监听面板数据的变化
        serializedObject.Update();

        // === 1. 第一区块：动态过滤显示 ===
        EditorGUILayout.LabelField("1. 谁来享受加成？ (Filter)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(filterProp);

        // 获取当前下拉菜单选了第几项
        TargetFilterType currentFilter = (TargetFilterType)filterProp.enumValueIndex;

        // 核心魔法：根据选项，决定画出哪个输入框！
        if (currentFilter == TargetFilterType.ByTag)
        {
            EditorGUILayout.PropertyField(targetTagProp);
        }
        else if (currentFilter == TargetFilterType.ByType)
        {
            EditorGUILayout.PropertyField(targetTypeProp);
        }
        // 如果选了 All 或者 Self，这两个框就凭空消失了！

        EditorGUILayout.Space(); // 空一行，保持美观

        // === 2. 第二区块：修改什么属性 ===
        EditorGUILayout.LabelField("2. 修改什么属性？ (Target Stat)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetStatProp);

        EditorGUILayout.Space();

        // === 3. 第三区块：怎么改 ===
        EditorGUILayout.LabelField("3. 怎么改？ (Operation)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(operationProp);
        EditorGUILayout.PropertyField(valueProp);

        // 应用修改，保存数据
        serializedObject.ApplyModifiedProperties();
    }
}
#endif