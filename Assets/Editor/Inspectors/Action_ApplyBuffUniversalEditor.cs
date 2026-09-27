#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(Action_ApplyBuffUniversal))]
public class Action_ApplyBuffUniversalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("BuffToApply"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Stacks"));

        var targetMode = (BuffTargetMode)serializedObject.FindProperty("TargetMode").enumValueIndex;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetMode"));

        if (targetMode == BuffTargetMode.Area)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("范围模式专属参数", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CenterType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("FactionFilter"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Radius"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif