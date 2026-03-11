using UnityEngine;
using UnityEditor; // 引入强大的编辑器扩展库

// 告诉 Unity：这个脚本是用来专门定制 ChassisSetupHelper 在编辑器里的表现的
[CustomEditor(typeof(ChassisSetupHelper))]
public class ChassisSetupHelperEditor : Editor
{
    // OnSceneGUI 允许我们在 Scene 场景窗口里画东西、加交互手柄
    private void OnSceneGUI()
    {
        // 获取当前选中的测试治具
        ChassisSetupHelper helper = (ChassisSetupHelper)target;

        // 安全检查：如果数据没填全，就不画手柄
        if (helper.TargetChassis == null || helper.TestComponent == null || helper.TargetChassis.Sockets.Count <= helper.TestSlotIndex)
            return;

        var slot = helper.TargetChassis.Sockets[helper.TestSlotIndex];

        // 1. 获取“转轴(Hinge)”在真实世界里的坐标和旋转角度
        Vector3 hingeWorldPos = helper.transform.TransformPoint(slot.LocalPosition);
        Quaternion hingeRot = helper.transform.rotation * Quaternion.Euler(0, 0, slot.MountAngle + helper.TestComponent.BaseRotationOffset);

        // 2. 获取当前的缩放比例
        float finalScale = slot.DefaultComponentScale * helper.TestComponent.VisualScaleMultiplier * helper.GlobalVisualScale;

        // 3. 计算出“组件图片正中心”现在应该在世界坐标的哪个位置
        // （数学原理：位置 = 转轴位置 + 旋转后的反向偏移量）
        Vector3 currentWorldOffset = hingeRot * (-helper.TestComponent.AnchorOffset * finalScale);
        Vector3 spriteCenterWorld = hingeWorldPos + currentWorldOffset;

        // 准备开始监听你在屏幕上的拖拽操作
        EditorGUI.BeginChangeCheck();

        // 【极其核心】在图片中心画一个你可以拖拽的 3D 坐标轴手柄（Handles）
        Vector3 newSpriteCenterWorld = Handles.PositionHandle(spriteCenterWorld, hingeRot);

        // 如果你用鼠标拖动了那个手柄...
        if (EditorGUI.EndChangeCheck())
        {
            // 记录一次撤销操作（允许你按 Ctrl+Z 还原）
            Undo.RecordObject(helper.TestComponent, "Adjust Anchor Offset");

            // 逆向计算：将你拖动后的世界坐标，反算回局部的 AnchorOffset 偏移量
            Vector3 localDelta = Quaternion.Inverse(hingeRot) * (newSpriteCenterWorld - hingeWorldPos);
            Vector2 newAnchorOffset = -localDelta / finalScale;

            // 把算出来的精确数字，直接覆盖写回你的 ComponentDataSO 数据文件里！
            helper.TestComponent.AnchorOffset = newAnchorOffset;

            // 告诉 Unity：这个文件被修改了，记得保存！
            EditorUtility.SetDirty(helper.TestComponent);

            // 强制刷新屏幕画面
            helper.SyncPreviewTransforms();
        }

        // 为了视觉清晰，画一条粉色的虚线，连接“插槽点”和“图片的几何中心”
        Handles.color = Color.magenta;
        Handles.DrawDottedLine(hingeWorldPos, newSpriteCenterWorld, 4f);

        // 在手柄旁边写个提示
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(newSpriteCenterWorld + Vector3.up * 0.5f, " ← 拖动此轴微调齿轮点", style);
    }
}