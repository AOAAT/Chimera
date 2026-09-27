// --- WorldBackgroundScaler.cs ---
using UnityEngine;

[ExecuteInEditMode]
public class WorldBackgroundScaler : MonoBehaviour
{
    private SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    // 在运行时和编辑器下都实时校准
    void LateUpdate()
    {
        if (sr == null || sr.sprite == null || Camera.main == null) return;

        // 1. 获取摄像机的可视区域高度和宽度
        float worldScreenHeight = Camera.main.orthographicSize * 2.0f;
        float worldScreenWidth = worldScreenHeight * ((float)Screen.width / Screen.height);

        // 2. 背景贴图原始尺寸
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        // 3. 计算覆盖倍率（取 Max 确保填满，哪怕切掉一点边缘）
        float scaleX = worldScreenWidth / spriteWidth;
        float scaleY = worldScreenHeight / spriteHeight;
        float finalScale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }
}