using UnityEngine;

public class PersistentWaypoint : MonoBehaviour
{
    void Update()
    {
        // 1. 缓缓自转
        transform.Rotate(Vector3.forward, 90f * Time.deltaTime);

        // 2. 呼吸缩放效果
        float pulse = 0.8f + Mathf.PingPong(Time.time * 2f, 0.4f);
        transform.localScale = Vector3.one * pulse;
    }
}