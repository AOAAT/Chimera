using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScrapFlyEffect : MonoBehaviour
{
    public void Play(Sprite scrapSprite, Vector3 startPos, Vector3 endPos)
    {
        transform.position = startPos;
        GetComponent<Image>().sprite = scrapSprite;
        StartCoroutine(ScrapRoutine(startPos, endPos));
    }

    private IEnumerator ScrapRoutine(Vector3 start, Vector3 end)
    {
        float elapsed = 0f;
        float duration = Random.Range(0.6f, 1.0f); // 每个颗粒速度不一

        // --- 1. 爆炸阶段：先随机弹开 ---
        Vector3 burstPos = start + (Vector3)(Random.insideUnitCircle * 150f);
        float burstDuration = 0.2f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(start, burstPos, elapsed / burstDuration);
            yield return null;
        }

        // --- 2. 贝塞尔吸附阶段 ---
        elapsed = 0f;
        float flyDuration = duration - burstDuration;
        Vector3 midPoint = (transform.position + end) / 2f + (Vector3)Random.insideUnitCircle * 100f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flyDuration;

            // 二阶贝塞尔
            transform.position = Mathf.Pow(1 - t, 2) * burstPos +
                                 2 * (1 - t) * t * midPoint +
                                 Mathf.Pow(t, 2) * end;

            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.4f, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}