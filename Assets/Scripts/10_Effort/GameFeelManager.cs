using System.Collections;
using UnityEngine;

public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance;

    private Coroutine hitStopCoroutine;

    // 👇 修改 1：把它公开为一个只读属性，其他脚本（比如 UI）可以查询它
    public bool IsFrozen { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RequestHitStop(float duration, float timeScale = 0.02f)
    {
        // 👇 修改 2：如果当前已经在冻结中，且新请求的时间没有更长，就忽略它
        // 这样可以防止重机枪扫射时，游戏因为密集的 HitStop 彻底动弹不得
        if (IsFrozen && hitStopCoroutine != null) return;

        if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, timeScale));
    }

    private IEnumerator HitStopRoutine(float duration, float targetScale)
    {
        IsFrozen = true; // 开始冻结
        float originalScale = 1.0f;

        Time.timeScale = targetScale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = originalScale;
        IsFrozen = false; // 结束冻结
        hitStopCoroutine = null;
    }
}