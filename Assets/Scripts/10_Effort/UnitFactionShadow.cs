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
        // 【核心修复】：跨层级寻找，防止因为 SetParent 导致 Find 失败从而无限生成
        if (shadowTransform == null)
        {
            shadowTransform = GetComponentInChildren<SpriteRenderer>()?.gameObject.name == "Logic_Visual_Shadow"
                ? GetComponentInChildren<SpriteRenderer>().transform
                : transform.Find("Logic_Visual_Shadow");

            if (shadowTransform == null)
            {
                GameObject shadowObj = new GameObject("Logic_Visual_Shadow");
                shadowTransform = shadowObj.transform;
                shadowTransform.SetParent(this.transform, false);
                shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
                shadowObj.layer = LayerMask.NameToLayer("Default");
                shadowRenderer.sortingOrder = -50;
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

    public void SetupModularShadow(bool isEnemy, float unitWidth, float bottomY)
    {
        EnsureShadowObject();
        ApplyStandardSettings(isEnemy);
        shadowTransform.localPosition = new Vector3(0f, bottomY, 0.15f); // Z轴稍作偏移
        float sWidth = unitWidth * 1.15f;
        shadowTransform.localScale = new Vector3(sWidth, sWidth * 0.35f, 1f);
    }

    public void SetupManualShadow(bool isEnemy, float width, float height, Vector2 offset)
    {
        EnsureShadowObject();
        ApplyStandardSettings(isEnemy);
        shadowTransform.localPosition = new Vector3(offset.x, offset.y, 0.15f);
        shadowTransform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplyStandardSettings(bool isEnemy)
    {
        targetFactionColor = isEnemy ? new Color(1f, 0f, 0f, 0.25f) : new Color(0f, 1f, 0f, 0.25f);
        if (shadowRenderer == null) shadowRenderer = shadowTransform.GetComponent<SpriteRenderer>();

        shadowRenderer.sprite = ShadowSprite;
        shadowRenderer.color = targetFactionColor;

        // 动态对齐父级排序
        var parentSR = shadowTransform.parent.GetComponent<SpriteRenderer>();
        if (parentSR != null)
        {
            shadowRenderer.sortingLayerName = parentSR.sortingLayerName;
            shadowRenderer.sortingOrder = parentSR.sortingOrder + SHADOW_ORDER_OFFSET;
        }
    }

    private void LateUpdate()
    {
        if (shadowRenderer != null) shadowRenderer.color = targetFactionColor;
    }
}