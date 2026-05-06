// --- START OF FILE EntityHUDSpawner.cs ---
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(DamageReceiver))]
[RequireComponent(typeof(BuffManager))]
public class EntityHUDSpawner : MonoBehaviour
{
    [Tooltip("拖入做好的 World Space 实体 HUD 预制体")]
    public GameObject HUDPrefab;

    [Tooltip("在算出最高点后，额外向上偏移的缓冲距离")]
    public float PaddingY = 0.5f;

    private GameObject hudObj;
    // --- 修改 EntityHUDSpawner.cs 的 Start 方法 ---

    private void Start()
    {
        if (HUDPrefab == null) return;

        // 1. 实例化血条
        hudObj = Instantiate(HUDPrefab, this.transform);

        // ==========================================
        // 👇【核心修正】：基于原始缩放的补偿
        // ==========================================
        // 获取预制体身上那个 0.01 左右的原始微小缩放
        Vector3 originalTinyScale = HUDPrefab.transform.localScale;
        // 获取单位（怪物/机甲）当前的全局缩放
        Vector3 unitLossyScale = transform.lossyScale;

        // 最终缩放 = 原始微小缩放 / 单位缩放
        // 这样 0.01 (原始) / 1.5 (单位) = 0.0066... (在视觉上看起来就是 0.01)
        hudObj.transform.localScale = new Vector3(
            originalTinyScale.x / unitLossyScale.x,
            originalTinyScale.y / unitLossyScale.y,
            originalTinyScale.z / unitLossyScale.z
        );
        // ==========================================

        // 2. 智能计算高度偏移（原有逻辑保持不变）
        float heightOffset = 1.5f;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        if (srs.Length > 0)
        {
            float maxY = float.MinValue;
            foreach (var sr in srs)
            {
                if (sr.transform.IsChildOf(hudObj.transform)) continue;
                // 注意：这里需要反向应用缩放，否则位置会飘
                float localTop = transform.InverseTransformPoint(sr.bounds.max).y;
                if (localTop > maxY) maxY = localTop;
            }
            if (maxY != float.MinValue) heightOffset = maxY + PaddingY;
        }

        // 应用自适应高度
        hudObj.transform.localPosition = new Vector3(0, heightOffset, 0);

        // 3. 初始化数据与监听
        EntityHUD hudScript = hudObj.GetComponent<EntityHUD>();
        if (hudScript != null)
        {
            hudScript.Initialize(GetComponent<DamageReceiver>(), GetComponent<BuffManager>());
            EnemyBrain brain = GetComponent<EnemyBrain>();
            if (brain != null) brain.SetHUD(hudScript);
        }

        // 4. 深度排序（这里如果还贴脸，可能是层级问题，建议 Sorting Order 设为 50-100 即可）
        Canvas canvas = hudObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "Entities";
            canvas.sortingOrder = 100; // 调高一点，确保在最上层
        }
    }
}