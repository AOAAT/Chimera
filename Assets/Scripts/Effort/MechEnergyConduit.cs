using UnityEngine;
using UnityEngine.UI;

public class MechEnergyConduit : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Image uiLine; // 如果在UI中使用

    private Transform startPoint;
    private Transform endPoint;

    [Header("=== 视觉配置 ===")]
    public Color BaseColor = new Color(0, 0.5f, 1f, 0.4f);
    public Color PulseColor = new Color(0, 1f, 1f, 1f);
    public float FlowSpeed = 1.5f;

    private float pulseTimer = 0f;
    private Material lineMat;

    public void Initialize(Transform start, Transform end)
    {
        lineRenderer = GetComponent<LineRenderer>();
        startPoint = start;
        endPoint = end;

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineMat = lineRenderer.material;
            // 确保渲染在 Entities 层，但要在底盘和武器之间
            lineRenderer.sortingLayerName = "Entities";
            lineRenderer.sortingOrder = 5;
        }
    }

    private void Update()
    {
        if (startPoint == null || endPoint == null) return;

        // 更新位置
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, startPoint.position);
            lineRenderer.SetPosition(1, endPoint.position);

            // 模拟纹理流动
            float offset = Time.time * FlowSpeed;
            lineMat.SetTextureOffset("_MainTex", new Vector2(-offset, 0));

            // 处理脉冲闪烁
            if (pulseTimer > 0)
            {
                pulseTimer -= Time.deltaTime * 4f;
                lineRenderer.startColor = lineRenderer.endColor = Color.Lerp(BaseColor, PulseColor, pulseTimer);
                lineRenderer.startWidth = Mathf.Lerp(0.05f, 0.15f, pulseTimer);
            }
            else
            {
                lineRenderer.startColor = lineRenderer.endColor = BaseColor;
                lineRenderer.startWidth = 0.05f;
            }
        }
    }

    public void TriggerPulse() { pulseTimer = 1.0f; }
}