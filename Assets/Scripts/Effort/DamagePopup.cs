// --- START OF FILE DamagePopup.cs ---
using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TMP_Text textMesh;
    private float disappearTimer;
    private float moveYSpeed = 2f;
    private Color textColor;
    private Vector3 baseScale;

    // --- 请替换 DamagePopup.cs 中的 Setup 方法 ---

    public void Setup(float damageAmount, bool isCrit, bool isTrueDamage, bool isArmorAbsorb, bool isPlayerTakeDamage)
    {
        textMesh = GetComponent<TMP_Text>();

        // 强制最高渲染层级
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = 30000;
        }

        textMesh.text = Mathf.CeilToInt(damageAmount).ToString();

        // 👇【核心修改】：全新的颜色优先级逻辑！
        if (isArmorAbsorb)
            textColor = new Color(0.6f, 0.7f, 0.8f, 1f); // 1. 护甲 (AP) 损耗：灰蓝色
        else if (isTrueDamage)
            textColor = new Color(0.8f, 0.2f, 0.8f, 1f); // 2. 真实伤害：高贵的紫色
        else if (isCrit)
            textColor = new Color(1f, 0.8f, 0f, 1f);     // 3. 暴击伤害：金黄色
        else
            textColor = new Color(1f, 0.2f, 0.2f, 1f);   // 4. 普通血量 (HP) 伤害：刀刀见血的红色！

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

        // 随机错开位置，如果一发子弹同时打掉 AP 和 HP，两个数字会分开弹出！
        transform.position += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
        disappearTimer = 0.8f;
    }

    private void Update()
    {
        transform.position += Vector3.up * moveYSpeed * Time.deltaTime;
        disappearTimer -= Time.deltaTime;

        if (disappearTimer < 0.4f)
        {
            // 完美透明度渐变
            float fadeAlpha = disappearTimer / 0.4f;
            textColor.a = fadeAlpha;
            textMesh.color = textColor;

            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * 0.5f, Time.deltaTime * 5f);
        }

        if (disappearTimer <= 0) Destroy(gameObject);
    }
}