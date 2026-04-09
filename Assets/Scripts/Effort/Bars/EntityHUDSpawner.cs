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

    private void Start()
    {
        if (HUDPrefab == null) return;

        // 1. 实例化血条
        hudObj = Instantiate(HUDPrefab, this.transform);

        // 2. 👇【核心修复 1】：智能计算贴图的最高点 (Bounding Box Top)
        float heightOffset = 1.5f; // 兜底高度

        // 尝试寻找身上所有的贴图组件，取最高的一个边界
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        if (srs.Length > 0)
        {
            float maxY = float.MinValue;
            foreach (var sr in srs)
            {
                // 如果是血条自己的部件（比如 Buff 图标），跳过
                if (sr.transform.IsChildOf(hudObj.transform)) continue;

                // 计算该贴图在局部坐标系下的最高点 (bounds.max.y 换算到 local)
                float localTop = transform.InverseTransformPoint(sr.bounds.max).y;
                if (localTop > maxY) maxY = localTop;
            }
            if (maxY != float.MinValue)
            {
                heightOffset = maxY + PaddingY;
            }
        }

        // 应用自适应高度！无论是巨型 Boss 还是矮小履带，血条永远恰好在头顶！
        hudObj.transform.localPosition = new Vector3(0, heightOffset, 0);

        // 3. 初始化数据与监听
        EntityHUD hudScript = hudObj.GetComponent<EntityHUD>();
        if (hudScript != null)
        {
            hudScript.Initialize(GetComponent<DamageReceiver>(), GetComponent<BuffManager>());
        }

        // 4. 👇【核心修复 2】：接管深度排序！
        // 为血条的 Canvas 增加 SortingGroup，让它和本体共进退！
        Canvas canvas = hudObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            // 确保它是 World Space，并覆盖其默认的 Order in Layer
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "Entities"; // 必须和机甲/怪物同层
            canvas.sortingOrder = 50; // 给一个较高的基础 Order，保证在本体之上
        }

        // 给血条挂一个动态排序脚本，YOffset 设为负的当前高度，这样它计算出的深度和脚底板完全一致！
        DynamicDepthSorter sorter = hudObj.AddComponent<DynamicDepthSorter>();
        sorter.IsStatic = false;
        sorter.YOffset = -heightOffset;
    }
}