using UnityEngine;
using System.Collections.Generic;

public class WeaponRangeVisualizer : MonoBehaviour
{
    [Header("=== 基础配置 ===")]
    public int CircleSegments = 60;
    public float LineWidth = 0.04f;
    public Color MaxRangeColor = new Color(0, 1, 0, 0.5f); // 半透明绿
    public Color MinRangeColor = new Color(1, 0, 0, 0.6f); // 半透明红

    private List<LineRenderer> activeRenderers = new List<LineRenderer>();
    private bool isShowing = false;
    private Material defaultLineMaterial;

    private void Awake()
    {
        // 自动创建一个最简单的、支持颜色的基础材质
        // 这样策划就不需要手动去外面新建材质球了
        defaultLineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    public void SetVisible(bool visible)
    {
        isShowing = visible;
        if (!visible) ClearCircles();
        else UpdateRanges();
    }

    private void Update()
    {
        if (isShowing) UpdateRanges();
    }

    private void UpdateRanges()
    {
        WeaponModule[] weapons = GetComponentsInChildren<WeaponModule>();

        int neededRenderers = 0;
        foreach (var w in weapons)
        {
            neededRenderers++;
            if (w.GetWeaponData().GetStat(StatType.MinRange) > 0.1f) neededRenderers++;
        }

        AdjustRendererCount(neededRenderers);

        int rIdx = 0;
        float distMult = CombatSandbox.GetDist(1.0f);

        for (int i = 0; i < weapons.Length; i++)
        {
            var wData = weapons[i].GetWeaponData();

            // 1. 绘制 MaxRange (绿色)
            float maxR = wData.GetStat(StatType.MaxRange) * distMult;
            DrawCircle(activeRenderers[rIdx++], maxR, MaxRangeColor);

            // 2. 绘制 MinRange (红色)
            float minR = wData.GetStat(StatType.MinRange) * distMult;
            if (minR > 0.1f)
            {
                DrawCircle(activeRenderers[rIdx++], minR, MinRangeColor);
            }
        }
    }

    private void DrawCircle(LineRenderer lr, float radius, Color color)
    {
        lr.gameObject.SetActive(true);
        lr.positionCount = CircleSegments + 1;
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = LineWidth;

        for (int i = 0; i <= CircleSegments; i++)
        {
            float angle = (i / (float)CircleSegments) * Mathf.PI * 2;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            // 设为 0.1 确保在机甲脚底
            lr.SetPosition(i, transform.position + new Vector3(x, y, 0.1f));
        }
    }

    private void AdjustRendererCount(int count)
    {
        while (activeRenderers.Count < count)
        {
            GameObject go = new GameObject("Range_Circle");
            go.transform.SetParent(this.transform);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.material = defaultLineMaterial; // 使用自动生成的材质
            lr.sortingLayerName = "Entities";
            lr.sortingOrder = -1;
            activeRenderers.Add(lr);
        }

        for (int i = 0; i < activeRenderers.Count; i++)
        {
            activeRenderers[i].gameObject.SetActive(i < count);
        }
    }

    private void ClearCircles()
    {
        foreach (var lr in activeRenderers) lr.gameObject.SetActive(false);
    }
}