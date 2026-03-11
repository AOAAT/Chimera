using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(SpriteRenderer))]
public class ChassisSetupHelper : MonoBehaviour
{
    public ChassisDataSO TargetChassis;

    [Header("整体环境缩放")]
    [Range(0.1f, 10f)]
    public float GlobalVisualScale = 1.0f;

    [Header("实体预览调试")]
    public ComponentDataSO TestComponent;
    public int TestSlotIndex = 0;

    [SerializeField, HideInInspector]
    private GameObject previewObject;

    private SpriteRenderer chassisRenderer;
    private bool needsUpdate = false;

    private void OnValidate()
    {
        needsUpdate = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            // 【核心修复】：在编辑器模式下，每帧实时同步位置和角度！
            // 无论你在哪里改了数据，转轴和贴图都会立刻响应，无需重建实体。
            SyncPreviewTransforms();

            if (needsUpdate)
            {
                UpdateVisuals();
                needsUpdate = false;
            }
        }
    }

    public void UpdateVisuals()
    {
        if (TargetChassis == null)
        {
            ClearPreview();
            return;
        }

        transform.localScale = Vector3.one * GlobalVisualScale;
        if (chassisRenderer == null) chassisRenderer = GetComponent<SpriteRenderer>();
        if (chassisRenderer != null) chassisRenderer.sprite = TargetChassis.ChassisSprite;

        HandleComponentPreviewSafe();
    }

    private void HandleComponentPreviewSafe()
    {
        if (TestComponent == null || TargetChassis.Sockets == null || TestSlotIndex >= TargetChassis.Sockets.Count)
        {
            ClearPreview();
            return;
        }

        // 只有当你彻底更换了组件或插槽时，我们才执行“销毁重建”
        ClearPreview();

        // 1. 创建“转轴 (Hinge)”
        previewObject = new GameObject("PREVIEW_HINGE");
        previewObject.hideFlags = HideFlags.DontSave;
        previewObject.transform.SetParent(this.transform);

        // 2. 创建真正的“贴图实体”
        GameObject spriteObj = new GameObject("Sprite_Visual");
        spriteObj.transform.SetParent(previewObject.transform);

        SpriteRenderer cpRenderer = spriteObj.AddComponent<SpriteRenderer>();
        cpRenderer.sprite = TestComponent.ComponentIcon;
        cpRenderer.sortingOrder = chassisRenderer.sortingOrder + 1;

        // 生成后立刻同步一次位置
        SyncPreviewTransforms();
    }

    // 【新增的核心公共函数】：不销毁物体，只刷新物理姿态
    public void SyncPreviewTransforms()
    {
        if (previewObject == null || TargetChassis == null || TestComponent == null) return;
        if (TargetChassis.Sockets == null || TestSlotIndex >= TargetChassis.Sockets.Count) return;

        var slot = TargetChassis.Sockets[TestSlotIndex];

        // 1. 实时同步“转轴”的角度和缩放
        float totalAngle = slot.MountAngle + TestComponent.BaseRotationOffset;
        previewObject.transform.localPosition = slot.LocalPosition;
        previewObject.transform.localRotation = Quaternion.Euler(0, 0, totalAngle);

        float finalScale = slot.DefaultComponentScale * TestComponent.VisualScaleMultiplier;
        previewObject.transform.localScale = Vector3.one * finalScale;

        // 2. 实时同步“贴图实体”的反向偏移
        if (previewObject.transform.childCount > 0)
        {
            Transform spriteObj = previewObject.transform.GetChild(0);
            spriteObj.localPosition = -TestComponent.AnchorOffset;

            // 防呆设计：如果你突然换了图片素材，也让它实时刷新
            SpriteRenderer sr = spriteObj.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != TestComponent.ComponentIcon)
            {
                sr.sprite = TestComponent.ComponentIcon;
            }
        }
    }

    private void ClearPreview()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
            previewObject = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (TargetChassis == null || TargetChassis.Sockets == null) return;

        for (int i = 0; i < TargetChassis.Sockets.Count; i++)
        {
            var slot = TargetChassis.Sockets[i];
            bool isTesting = (i == TestSlotIndex);

            Vector3 worldPos = transform.TransformPoint(slot.LocalPosition);
            Gizmos.color = isTesting ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.1f * GlobalVisualScale);

#if UNITY_EDITOR
            Vector3 direction = Quaternion.Euler(0, 0, slot.MountAngle) * Vector3.up;
            Vector3 worldDir = transform.TransformDirection(direction);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(worldPos, worldDir * 0.5f * GlobalVisualScale);
#endif
        }
    }

    private void OnDisable() { ClearPreview(); }
}