using System.Collections.Generic;
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

    [Header("多组件装配阵列 (自动与底盘插槽同步)")]
    // 用数组来接收多个组件
    public ComponentDataSO[] EquippedComponents = new ComponentDataSO[0];

    // 用列表来管理生成的多个预览转轴
    [SerializeField, HideInInspector]
    private List<GameObject> previewObjects = new List<GameObject>();

    private SpriteRenderer chassisRenderer;
    private bool needsUpdate = false;

    private void OnValidate()
    {
        // 【智能阵列同步】：自动检测底盘有几个插槽，就给你开几个测试框
        if (TargetChassis != null && TargetChassis.Sockets != null)
        {
            if (EquippedComponents.Length != TargetChassis.Sockets.Count)
            {
                System.Array.Resize(ref EquippedComponents, TargetChassis.Sockets.Count);
            }
        }
        needsUpdate = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
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
            ClearPreviews();
            return;
        }

        transform.localScale = Vector3.one * GlobalVisualScale;
        if (chassisRenderer == null) chassisRenderer = GetComponent<SpriteRenderer>();
        if (chassisRenderer != null) chassisRenderer.sprite = TargetChassis.ChassisSprite;

        HandleComponentPreviewSafe();
    }

    private void HandleComponentPreviewSafe()
    {
        ClearPreviews();

        if (TargetChassis.Sockets == null) return;

        // 遍历所有插槽，如果该槽位填了组件，就生成实体
        for (int i = 0; i < TargetChassis.Sockets.Count; i++)
        {
            // 塞一个空位，保证列表索引和插槽索引严格对应
            previewObjects.Add(null);

            // 如果这个格子没放组件数据，就跳过
            if (i >= EquippedComponents.Length || EquippedComponents[i] == null) continue;

            var comp = EquippedComponents[i];

            // 1. 生成转轴
            GameObject hingeObj = new GameObject($"PREVIEW_HINGE_[{i}]");

            hingeObj.layer = this.gameObject.layer;

            if (!Application.isPlaying)
            {
                // 如果没运行游戏，它是全息投影，不保存
                hingeObj.hideFlags = HideFlags.DontSave;
            }
            // 如果正在运行游戏，它就是真实的物理节点，永远存在！

            hingeObj.transform.SetParent(this.transform);

            // 2. 生成贴图
            GameObject spriteObj = new GameObject("Sprite_Visual");

            spriteObj.layer = this.gameObject.layer;
            spriteObj.transform.SetParent(hingeObj.transform);

            SpriteRenderer cpRenderer = spriteObj.AddComponent<SpriteRenderer>();
            cpRenderer.sprite = comp.ComponentIcon;

            // 巧妙的层级处理：后装的组件稍微靠前一点，防止多组件Z轴闪烁
            cpRenderer.sortingOrder = chassisRenderer.sortingOrder + 1 + i;

            // 存入列表
            previewObjects[i] = hingeObj;
        }

        SyncPreviewTransforms();
    }

    public void SyncPreviewTransforms()
    {
        if (TargetChassis == null || TargetChassis.Sockets == null) return;

        // 批量同步所有存活组件的位置和角度
        for (int i = 0; i < TargetChassis.Sockets.Count; i++)
        {
            if (i >= previewObjects.Count || previewObjects[i] == null) continue;
            if (i >= EquippedComponents.Length || EquippedComponents[i] == null) continue;

            var comp = EquippedComponents[i];
            var slot = TargetChassis.Sockets[i];
            var hingeObj = previewObjects[i];

            // 同步转轴
            float totalAngle = slot.MountAngle + comp.BaseRotationOffset;
            hingeObj.transform.localPosition = slot.LocalPosition;
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, totalAngle);

            float finalScale = slot.DefaultComponentScale * comp.VisualScaleMultiplier;
            hingeObj.transform.localScale = Vector3.one * finalScale;

            // 同步图片反向偏移
            if (hingeObj.transform.childCount > 0)
            {
                Transform spriteObj = hingeObj.transform.GetChild(0);
                spriteObj.localPosition = -comp.AnchorOffset;

                SpriteRenderer sr = spriteObj.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != comp.ComponentIcon)
                {
                    sr.sprite = comp.ComponentIcon;
                }
            }
        }
    }

    private void ClearPreviews()
    {
        foreach (var obj in previewObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        previewObjects.Clear();
    }

    // --- 替换 ChassisSetupHelper.cs 中的 OnDrawGizmos ---
    private void OnDrawGizmos()
    {
        if (TargetChassis == null || TargetChassis.Sockets == null) return;

        for (int i = 0; i < TargetChassis.Sockets.Count; i++)
        {
            var slot = TargetChassis.Sockets[i];
            bool isEquipped = (i < EquippedComponents.Length && EquippedComponents[i] != null);
            Vector3 worldPos = transform.TransformPoint(slot.LocalPosition);

            // 1. 画插槽
            Gizmos.color = isEquipped ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.1f * GlobalVisualScale);

            // 2. 👇【主策专属：画枪口十字星！】
            if (isEquipped && EquippedComponents[i].Type == ComponentType.Weapon)
            {
                if (i < previewObjects.Count && previewObjects[i] != null)
                {
                    Transform hinge = previewObjects[i].transform;
                    // 读取你在图纸里配置的枪口偏移，直接换算成世界坐标！
                    Vector3 muzzlePos = hinge.TransformPoint(EquippedComponents[i].MuzzleOffset);

                    Gizmos.color = Color.red; // 危险的红色准星
                    Gizmos.DrawWireSphere(muzzlePos, 0.08f * GlobalVisualScale);
                    // 画十字准星
                    float crossSize = 0.2f * GlobalVisualScale;
                    Gizmos.DrawLine(muzzlePos + Vector3.up * crossSize, muzzlePos + Vector3.down * crossSize);
                    Gizmos.DrawLine(muzzlePos + Vector3.left * crossSize, muzzlePos + Vector3.right * crossSize);
                }
            }

#if UNITY_EDITOR
            Vector3 direction = Quaternion.Euler(0, 0, slot.MountAngle) * Vector3.up;
            Vector3 worldDir = transform.TransformDirection(direction);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(worldPos, worldDir * 0.5f * GlobalVisualScale);
#endif
        }
    }

    private void OnDisable() { ClearPreviews(); }
}