using UnityEngine;

public class UnitFactionShadow : MonoBehaviour
{
    public Sprite ShadowSprite;
    private SpriteRenderer shadowRenderer;
    private Transform shadowTransform;
    private Color targetFactionColor;
    private const int SHADOW_ORDER_OFFSET = -100;

    public void EnsureShadowObject()
    {
        if (shadowTransform == null)
        {
            // 1. 尝试在当前物体及子物体中寻找已有的阴影，避免重复生成
            shadowTransform = transform.Find("Logic_Visual_Shadow");

            if (shadowTransform == null)
            {
                GameObject shadowObj = new GameObject("Logic_Visual_Shadow");
                shadowTransform = shadowObj.transform;
                // 初始化时先挂在自己身上，防止丢失
                shadowTransform.SetParent(this.transform, false);
                shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
                shadowObj.layer = LayerMask.NameToLayer("Default");
            }
            else
            {
                shadowRenderer = shadowTransform.GetComponent<SpriteRenderer>();
            }
        }
    }

    public Transform GetShadowTransform()
    {
        EnsureShadowObject();
        return shadowTransform;
    }

    // 模式 A：组装机甲用 (自动比例)
    public void SetupModularShadow(bool isEnemy, float unitWidth, float bottomY)
    {
        EnsureShadowObject();
        ApplyStandardSettings(isEnemy);
        shadowTransform.localPosition = new Vector3(0f, bottomY, 0.1f);
        float sWidth = unitWidth * 1.15f;
        shadowTransform.localScale = new Vector3(sWidth, sWidth * 0.35f, 1f);
    }

    // 模式 B：手动复写用
    public void SetupManualShadow(bool isEnemy, float width, float height, Vector2 offset)
    {
        EnsureShadowObject();
        ApplyStandardSettings(isEnemy);
        shadowTransform.localPosition = new Vector3(offset.x, offset.y, 0.1f);
        shadowTransform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplyStandardSettings(bool isEnemy)
    {
        targetFactionColor = isEnemy ? new Color(1f, 0f, 0f, 0.25f) : new Color(0f, 1f, 0f, 0.25f);
        shadowRenderer.sprite = ShadowSprite;
        shadowRenderer.color = targetFactionColor;
        // 关键：寻找真正的渲染父级（通常是底盘或怪物的 Sprite 节点）
        var parentSR = shadowTransform.parent.GetComponent<SpriteRenderer>();
        if (parentSR != null)
        {
            shadowRenderer.sortingLayerName = parentSR.sortingLayerName;
            shadowRenderer.sortingOrder = parentSR.sortingOrder + SHADOW_ORDER_OFFSET;
        }
        else
        {
            shadowRenderer.sortingLayerName = "Entities";
            shadowRenderer.sortingOrder = -50;
        }
    }
    private void LateUpdate()
    {
        if (shadowRenderer != null && shadowRenderer.color != targetFactionColor)
        {
            shadowRenderer.color = targetFactionColor;
        }
    }
}