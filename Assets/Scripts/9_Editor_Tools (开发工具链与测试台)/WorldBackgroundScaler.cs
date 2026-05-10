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
        if (sr == null || sr.sprite == null) return;

        // 1. 获取摄像机的高度（世界单位）
        // OrthographicSize 是屏幕高度的一半，所以 * 2
        float worldScreenHeight = Camera.main.orthographicSize * 2.0f;

        // 2. 获取摄像机的宽度（世界单位）
        float worldScreenWidth = worldScreenHeight / Screen.height * Screen.width;

        // 3. 获取 Sprite 的原始尺寸
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        // 4. 计算缩放倍率，取最大值（确保填满屏幕，宁可裁剪掉一点边缘，也不留黑边）
        float scaleX = worldScreenWidth / spriteWidth;
        float scaleY = worldScreenHeight / spriteHeight;

        float finalScale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }
}