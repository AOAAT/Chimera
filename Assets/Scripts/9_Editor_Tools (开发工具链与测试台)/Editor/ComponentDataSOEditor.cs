#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComponentDataSO))]
public class ComponentDataSOEditor : Editor
{
    // 基础引用
    SerializedProperty componentBaseID, componentName, description, componentIcon, type;
    SerializedProperty tacticalRoleDesc, animController, muzzleOffset;
    SerializedProperty macroCategory, baseSubTags, minDropLevel, levelMatrix;

    // 武器专属
    SerializedProperty deliveryType, projectilePrefab, targetingOverride;
    SerializedProperty windupAngle, strikeAngle, windupTimeRatio, strikeTimeRatio;

    // 视觉修正
    SerializedProperty anchorOffset, baseRotationOffset, visualScaleMultiplier;

    // 核心专属
    SerializedProperty targetingLogic, movementLogic, safeDodgeDistance;

    private void OnEnable()
    {
        // 绑定所有属性
        componentBaseID = serializedObject.FindProperty("ComponentBaseID");
        componentName = serializedObject.FindProperty("ComponentName");
        description = serializedObject.FindProperty("Description");
        componentIcon = serializedObject.FindProperty("ComponentIcon");
        type = serializedObject.FindProperty("Type");

        tacticalRoleDesc = serializedObject.FindProperty("TacticalRoleDesc");
        animController = serializedObject.FindProperty("AnimController");
        muzzleOffset = serializedObject.FindProperty("MuzzleOffset");

        macroCategory = serializedObject.FindProperty("MacroCategory");
        baseSubTags = serializedObject.FindProperty("BaseSubTags");
        minDropLevel = serializedObject.FindProperty("MinDropLevel");
        levelMatrix = serializedObject.FindProperty("LevelMatrix");

        deliveryType = serializedObject.FindProperty("DeliveryType");
        projectilePrefab = serializedObject.FindProperty("ProjectilePrefab");
        targetingOverride = serializedObject.FindProperty("TargetingOverride"); // 👈 新增绑定

        windupAngle = serializedObject.FindProperty("WindupAngle");
        strikeAngle = serializedObject.FindProperty("StrikeAngle");
        windupTimeRatio = serializedObject.FindProperty("WindupTimeRatio");
        strikeTimeRatio = serializedObject.FindProperty("StrikeTimeRatio");

        anchorOffset = serializedObject.FindProperty("AnchorOffset");
        baseRotationOffset = serializedObject.FindProperty("BaseRotationOffset");
        visualScaleMultiplier = serializedObject.FindProperty("VisualScaleMultiplier");

        targetingLogic = serializedObject.FindProperty("TargetingLogic");
        movementLogic = serializedObject.FindProperty("MovementLogic");
        safeDodgeDistance = serializedObject.FindProperty("SafeDodgeDistance");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. 基础身份信息
        EditorGUILayout.LabelField("🆔 基础身份 (Identity)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(componentBaseID);
        EditorGUILayout.PropertyField(componentName);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(componentIcon);

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.7f, 1f, 1f); // 给类型切换器上个色
        EditorGUILayout.PropertyField(type);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();

        ComponentType currentType = (ComponentType)type.enumValueIndex;

        // 2. 分支渲染逻辑
        if (currentType == ComponentType.Core)
        {
            RenderCoreSection();
        }
        else if (currentType == ComponentType.Weapon)
        {
            RenderWeaponSection();
        }
        else if (currentType == ComponentType.Movement)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("👣 移动组件阴影微调", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OverrideShadow"));
            if (serializedObject.FindProperty("OverrideShadow").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ShadowOffset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ShadowWidth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ShadowHeight"));
            }
            EditorGUILayout.EndVertical();
        }

        // 3. 表现层通用配置
        RenderVisualSection();

        // 4. 数据矩阵与标签
        RenderDataSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void RenderCoreSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🧠 核心指挥中枢 (Logic Center)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("作为机甲大脑，此处的设定将决定所有设置为 'FollowCoreAI' 武器的攻击偏好。", MessageType.Info);

        EditorGUILayout.PropertyField(targetingLogic);
        EditorGUILayout.PropertyField(movementLogic);
        EditorGUILayout.PropertyField(safeDodgeDistance);
        EditorGUILayout.EndVertical();
    }

    private void RenderWeaponSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚔️ 武器战斗配置 (Combat Specs)", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(tacticalRoleDesc);
        EditorGUILayout.Space();

        // 👇【核心增强】：索敌覆盖逻辑
        EditorGUILayout.LabelField("🎯 索敌偏好", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(targetingOverride);

        TargetingStrategy strategy = (TargetingStrategy)targetingOverride.enumValueIndex;
        if (strategy == TargetingStrategy.FollowCoreAI)
        {
            GUI.color = Color.gray;
            EditorGUILayout.LabelField("   ┕ 目前逻辑：无条件服从机甲核心大脑的指挥", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(1f, 0.8f, 0.4f); // 橘黄色
            EditorGUILayout.LabelField($"   ┕ 目前逻辑：战术抗命！强制执行 [{strategy}] 策略", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        EditorGUILayout.Space();
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

    private void RenderVisualSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("🎨 表现层修正 (Visual Adjustments)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(animController);
        EditorGUILayout.PropertyField(anchorOffset);
        EditorGUILayout.PropertyField(baseRotationOffset);
        EditorGUILayout.PropertyField(visualScaleMultiplier);
        EditorGUILayout.EndVertical();
    }

    private void RenderDataSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 数值矩阵 (Level Matrix)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(macroCategory);
        EditorGUILayout.PropertyField(baseSubTags);
        EditorGUILayout.PropertyField(minDropLevel);

        // 展开列表，显示 Level 1 ~ 4 的详细积木
        EditorGUILayout.PropertyField(levelMatrix, true);
        EditorGUILayout.EndVertical();
    }
}
#endif