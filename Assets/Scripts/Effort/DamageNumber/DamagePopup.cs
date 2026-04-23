using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TMP_Text textMesh;
    private float disappearTimer;
    private float moveYSpeed = 2f;
    private Color textColor;
    private Vector3 baseScale;

    // 【新增】：用于记录自己属于哪个对象池
    private GameObject mySourcePrefab;

    // 【关键修复】：增加第 6 个参数 sourcePrefab
    public void Setup(float damageAmount, bool isCrit, bool isTrueDamage, bool isArmorAbsorb, bool isPlayerTakeDamage, GameObject sourcePrefab)
    {
        this.mySourcePrefab = sourcePrefab; // 记住自己的来源
        textMesh = GetComponent<TMP_Text>();

        // 强制最高渲染层级
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = 30000;
        }

        textMesh.text = Mathf.CeilToInt(damageAmount).ToString();

        // 颜色优先级
        if (isArmorAbsorb)
            textColor = new Color(0.6f, 0.7f, 0.8f, 1f);
        else if (isTrueDamage)
            textColor = new Color(0.8f, 0.2f, 0.8f, 1f);
        else if (isCrit)
            textColor = new Color(1f, 0.8f, 0f, 1f);
        else
            textColor = new Color(1f, 0.2f, 0.2f, 1f);

        // 【对象池重要逻辑】：重置透明度，防止复用时是透明的
        textColor.a = 1f;
        textMesh.color = textColor;

        // 大小排版
        baseScale = Vector3.one * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
        baseScale *= 2f;

        if (isCrit)
        {
            textMesh.text += "!";
            transform.localScale = baseScale * 1.5f;
            textMesh.fontStyle = FontStyles.Bold;
        }
        else
        {
            transform.localScale = baseScale;
            textMesh.fontStyle = FontStyles.Normal;
        }

        disappearTimer = 0.8f;
    }

    private void Update()
    {
        transform.position += Vector3.up * moveYSpeed * Time.deltaTime;
        disappearTimer -= Time.deltaTime;

        if (disappearTimer < 0.4f)
        {
            float fadeAlpha = disappearTimer / 0.4f;
            textColor.a = fadeAlpha;
            textMesh.color = textColor;
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * 0.5f, Time.deltaTime * 5f);
        }

        if (disappearTimer <= 0)
        {
            // 【关键修复】：不再 Destroy，而是归还对象池
            if (mySourcePrefab != null)
                SimplePool.Despawn(mySourcePrefab, gameObject);
            else
                Destroy(gameObject); // 兜底，防止没传 prefab
        }
    }
}