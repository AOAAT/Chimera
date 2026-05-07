using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LootFlyEffect : MonoBehaviour
{
    private Image iconImage;
    private RectTransform rectTransform;

    public void Play(Sprite sprite, Vector3 startPos, Vector3 endPos, System.Action onComplete)
    {
        iconImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        iconImage.sprite = sprite;
        transform.position = startPos;

        StartCoroutine(FlyRoutine(startPos, endPos, onComplete));
    }

    private IEnumerator FlyRoutine(Vector3 start, Vector3 end, System.Action onComplete)
    {
        float duration = 0.8f; // 飞行总时长
        float elapsed = 0f;

        // --- 1. 计算贝塞尔控制点 (P1) ---
        // 取中点并向上+向随机侧边偏移，形成优美的弧线
        Vector3 midPoint = (start + end) / 2f;
        float randomOffset = Random.Range(-200f, 200f);
        Vector3 controlPoint = midPoint + Vector3.up * 300f + Vector3.right * randomOffset;

        // --- 2. 初始弹出效果 (Juice!) ---
        transform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // 二阶贝塞尔曲线公式: (1-t)^2*P0 + 2(1-t)*t*P1 + t^2*P2
            Vector3 currentPos = Mathf.Pow(1 - t, 2) * start +
                                 2 * (1 - t) * t * controlPoint +
                                 Mathf.Pow(t, 2) * end;

            transform.position = currentPos;

            // 缩放曲线：先变大再变小消失
            // 0 -> 0.2s: 0 to 1.2
            // 0.2 -> 0.8s: 1.2 to 0.1
            if (t < 0.2f)
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.2f, t / 0.2f);
            else
                transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one * 0.2f, (t - 0.2f) / 0.8f);

            // 旋转效果
            transform.Rotate(Vector3.forward, 360f * Time.unscaledDeltaTime);

            yield return null;
        }

        // --- 3. 落地反馈 ---
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}