using UnityEngine;

public class RTSClickIndicator : MonoBehaviour
{
    [Header("=== 抵达判定 ===")]
    public float ReachThreshold = 0.6f;

    [Header("=== 呼吸动效 ===")]
    public float RotationSpeed = 120f;
    public float PulseSpeed = 2f;
    public float PulseAmount = 0.2f;

    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;

        if (GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Generic_Click);

        // 深度对齐：确保在地面和机甲之间
        transform.position = new Vector3(transform.position.x, transform.position.y, -0.5f);
    }

    void Update()
    {
        // 1. 平滑自转
        transform.Rotate(Vector3.forward, RotationSpeed * Time.deltaTime);

        // 2. 呼吸缩放 (吸纳自 PersistentWaypoint)
        float pulse = 1.0f + Mathf.PingPong(Time.time * PulseSpeed, PulseAmount);
        transform.localScale = initialScale * pulse;

        // 3. 抵达检测逻辑
        if (BattleCommandManager.Instance != null)
        {
            var units = BattleCommandManager.Instance.SelectedUnits;
            foreach (var unit in units)
            {
                if (unit == null) continue;

                Vector2 unitPos2D = unit.transform.position;
                Vector2 myPos2D = transform.position;

                if (Vector2.Distance(unitPos2D, myPos2D) < ReachThreshold)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
        if (BattleCommandManager.Instance != null)
        {
            var mgr = BattleCommandManager.Instance;

            // 检查机甲是否到达
            foreach (var unit in mgr.SelectedUnits)
            {
                if (unit != null && Vector2.Distance(unit.transform.position, transform.position) < ReachThreshold)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // 检查居民是否到达 (新增)
            foreach (var res in mgr.SelectedResidents)
            {
                if (res != null && Vector2.Distance(res.transform.position, transform.position) < ReachThreshold)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}