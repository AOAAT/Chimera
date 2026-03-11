using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ChassisSetupHelper))]
public class ChassisSetupHelperEditor : Editor
{
    private void OnSceneGUI()
    {
        ChassisSetupHelper helper = (ChassisSetupHelper)target;

        if (helper.TargetChassis == null || helper.TargetChassis.Sockets == null) return;

        // 遍历所有插槽，并为每一个已安装的组件绘制独立的坐标手柄
        for (int i = 0; i < helper.TargetChassis.Sockets.Count; i++)
        {
            // 如果这个槽位没装东西，直接跳过
            if (i >= helper.EquippedComponents.Length || helper.EquippedComponents[i] == null) continue;

            var slot = helper.TargetChassis.Sockets[i];
            var comp = helper.EquippedComponents[i];

            // 获取转轴真实坐标
            Vector3 hingeWorldPos = helper.transform.TransformPoint(slot.LocalPosition);
            Quaternion hingeRot = helper.transform.rotation * Quaternion.Euler(0, 0, slot.MountAngle + comp.BaseRotationOffset);

            float finalScale = slot.DefaultComponentScale * comp.VisualScaleMultiplier * helper.GlobalVisualScale;

            Vector3 currentWorldOffset = hingeRot * (-comp.AnchorOffset * finalScale);
            Vector3 spriteCenterWorld = hingeWorldPos + currentWorldOffset;

            EditorGUI.BeginChangeCheck();

            // 在该组件上画出专属手柄
            Vector3 newSpriteCenterWorld = Handles.PositionHandle(spriteCenterWorld, hingeRot);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Adjust Anchor Offset");

                Vector3 localDelta = Quaternion.Inverse(hingeRot) * (newSpriteCenterWorld - hingeWorldPos);
                Vector2 newAnchorOffset = -localDelta / finalScale;

                comp.AnchorOffset = newAnchorOffset;
                EditorUtility.SetDirty(comp);

                // 拖动时，实时同步所有组件
                helper.SyncPreviewTransforms();
            }

            Handles.color = Color.magenta;
            Handles.DrawDottedLine(hingeWorldPos, newSpriteCenterWorld, 4f);

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            // 贴心地把插槽的名字打印在手柄旁边，防止你拖错对象
            Handles.Label(newSpriteCenterWorld + Vector3.up * 0.5f, $" ← 微调 [{slot.SlotName}]", style);
        }
    }
}