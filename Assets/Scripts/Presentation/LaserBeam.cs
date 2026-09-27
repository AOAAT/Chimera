// --- START OF FILE LaserBeam.cs ---
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserBeam : MonoBehaviour
{
    private LineRenderer line;
    private float lifeTimer;
    private float maxLifeTime;
    private float startWidth;

    public void Fire(Vector3 startPos, Vector3 endPos, float duration)
    {
        line = GetComponent<LineRenderer>();

        // 记录初始宽度用于淡出动画
        startWidth = line.startWidth;

        // 设置激光的起点(枪口)和终点(敌人)
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);

        maxLifeTime = duration;
        lifeTimer = duration;
    }

    private void Update()
    {
        if (line == null) return;

        lifeTimer -= Time.deltaTime;

        // 酷炫的淡出效果：激光会随着时间变细并消失
        float normalizedTime = lifeTimer / maxLifeTime;
        line.startWidth = startWidth * normalizedTime;
        line.endWidth = startWidth * normalizedTime;

        if (lifeTimer <= 0)
        {
            Destroy(gameObject);
        }
    }
}