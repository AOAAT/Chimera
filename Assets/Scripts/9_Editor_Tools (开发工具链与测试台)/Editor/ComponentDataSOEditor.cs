#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComponentDataSO))]
public class ComponentDataSOEditor : Editor
{
    // 定义所有的属性引用
    SerializedProperty componentBaseID, componentName, description, componentIcon, type;
    SerializedProperty tacticalRoleDesc, animController, muzzleOffset;
    SerializedProperty macroCategory, baseSubTags, minDropLevel, levelMatrix;
    SerializedProperty deliveryType, projectilePrefab;
    SerializedProperty windupAngle, strikeAngle, windupTimeRatio, strikeTimeRatio;
    SerializedProperty anchorOffset, baseRotationOffset, visualScaleMultiplier;
    SerializedProperty targetingLogic, movementLogic, safeDodgeDistance;

    private void OnEnable()
    {
        // 1. 基础身份
        componentBaseID = serializedObject.FindProperty("ComponentBaseID");
        componentName = serializedObject.FindProperty("ComponentName");
        description = serializedObject.FindProperty("Description");
        componentIcon = serializedObject.FindProperty("ComponentIcon");
        type = serializedObject.FindProperty("Type");

        // 2. 视觉与排布
        tacticalRoleDesc = serializedObject.FindProperty("TacticalRoleDesc");
        animController = serializedObject.FindProperty("AnimController");
        muzzleOffset = serializedObject.FindProperty("MuzzleOffset");
        anchorOffset = serializedObject.FindProperty("AnchorOffset");
        baseRotationOffset = serializedObject.FindProperty("BaseRotationOffset");
        visualScaleMultiplier = serializedObject.FindProperty("VisualScaleMultiplier");

        // 3. 标签与掉落
        macroCategory = serializedObject.FindProperty("MacroCategory");
        baseSubTags = serializedObject.FindProperty("BaseSubTags");
        minDropLevel = serializedObject.FindProperty("MinDropLevel");
        levelMatrix = serializedObject.FindProperty("LevelMatrix");

        // 4. 武器专属
        deliveryType = serializedObject.FindProperty("DeliveryType");
        projectilePrefab = serializedObject.FindProperty("ProjectilePrefab");
        windupAngle = serializedObject.FindProperty("WindupAngle");
        strikeAngle = serializedObject.FindProperty("StrikeAngle");
        windupTimeRatio = serializedObject.FindProperty("WindupTimeRatio");
        strikeTimeRatio = serializedObject.FindProperty("StrikeTimeRatio");

        // 5. 核心专属
        targetingLogic = serializedObject.FindProperty("TargetingLogic");
        movementLogic = serializedObject.FindProperty("MovementLogic");
        safeDodgeDistance = serializedObject.FindProperty("SafeDodgeDistance");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ==========================
        // 1. 公共头部 (所有组件可见)
        // ==========================
        EditorGUILayout.LabelField("--- 基础信息 (Identity) ---", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(componentBaseID);
        EditorGUILayout.PropertyField(componentName);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(componentIcon);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.cyan;
        EditorGUILayout.PropertyField(type);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();

        ComponentType currentType = (ComponentType)type.enumValueIndex;

        // ==========================
        // 2. 核心专属配置 (CORE ONLY)
        // ==========================
        if (currentType == ComponentType.Core)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🧠 核心独有 AI 设定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetingLogic);
            EditorGUILayout.PropertyField(movementLogic);
            EditorGUILayout.PropertyField(safeDodgeDistance);
            EditorGUILayout.EndVertical();
        }

        // ==========================
        // 3. 武器专属配置 (WEAPON ONLY)
        // ==========================
        if (currentType == ComponentType.Weapon)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("⚔️ 武器战斗配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(tacticalRoleDesc);
            EditorGUILayout.PropertyField(deliveryType);
            EditorGUILayout.PropertyField(muzzleOffset);

            WeaponDeliveryType delivery = (WeaponDeliveryType)deliveryType.enumValueIndex;

            if (delivery == WeaponDeliveryType.Ranged)
            {
                EditorGUILayout.PropertyField(projectilePrefab);
            }
            else if (delivery == WeaponDeliveryType.Melee)
            {
                EditorGUILayout.PropertyField(windupAngle);
                EditorGUILayout.PropertyField(strikeAngle);
                EditorGUILayout.PropertyField(windupTimeRatio);
                EditorGUILayout.PropertyField(strikeTimeRatio);
            }
            EditorGUILayout.EndVertical();
        }

        // ==========================
        // 4. 辅助/通用配置 (视觉与对齐)
        // ==========================
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("🎨 表现层 (视觉修正)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(animController);
        EditorGUILayout.PropertyField(anchorOffset);
        EditorGUILayout.PropertyField(baseRotationOffset);
        EditorGUILayout.PropertyField(visualScaleMultiplier);
        EditorGUILayout.EndVertical();

        // ==========================
        // 5. 经济与掉落 (LEVEL MATRIX)
        // ==========================
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 数值矩阵与掉落标签", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(macroCategory);
        EditorGUILayout.PropertyField(baseSubTags);
        EditorGUILayout.PropertyField(minDropLevel);
        EditorGUILayout.PropertyField(levelMatrix, true); // true 代表显示展开的内容
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif