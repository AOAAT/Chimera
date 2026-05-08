using UnityEngine;
using System.Collections;

public class TagPopEffect : MonoBehaviour
{
    public float PopDuration = 0.3f; // 弹出动画时长
    public AnimationCurve PopCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private void OnEnable()
    {
        // 每次生成时，先把自己缩到 0
        transform.localScale = Vector3.zero;
        StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        float elapsed = 0f;
        while (elapsed < PopDuration)
        {
            elapsed += Time.unscaledDeltaTime; // 使用 unscaled 确保卡肉时动画不卡
            float t = elapsed / PopDuration;

            // 计算缓动缩放
            float scale = PopCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;

            yield return null;
        }
        transform.localScale = Vector3.one;
    }
}