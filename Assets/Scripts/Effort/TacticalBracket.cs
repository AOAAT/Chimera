using UnityEngine;

public class TacticalBracket : MonoBehaviour
{
    private LineRenderer[] lines;
    public float BracketSize = 1.2f;
    public float LineLength = 0.4f;
    public Color NormalColor = new Color(0, 1, 1, 0.8f); // 青色 (搜索态)
    public Color LockColor = new Color(1, 0, 0, 1f);     // 红色 (锁定态)

    private float currentScale = 2f;
    private bool isLocked = false;

    private void Awake()
    {
        lines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject child = new GameObject($"Bracket_{i}");
            child.transform.SetParent(this.transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.startWidth = lr.endWidth = 0.04f;
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = 3; // L型需要3个点
            lines[i] = lr;
        }
        UpdateBracketPoints();
    }

    public void Show(bool lockedState)
    {
        isLocked = lockedState;
        gameObject.SetActive(true);
        currentScale = 1.8f; // 触发入场收缩动画
        SetColor(isLocked ? LockColor : NormalColor);
    }

    public void Hide() => gameObject.SetActive(false);

    private void SetColor(Color c)
    {
        foreach (var lr in lines) { lr.startColor = lr.endColor = c; }
    }

    private void Update()
    {
        if (currentScale > 1f)
        {
            currentScale = Mathf.Lerp(currentScale, 1f, Time.deltaTime * 15f);
            UpdateBracketPoints();
        }
        // 呼吸效果
        float pulse = 0.8f + Mathf.PingPong(Time.time * 2f, 0.2f);
        transform.localScale = Vector3.one * pulse;
    }

    private void UpdateBracketPoints()
    {
        float s = BracketSize * currentScale;
        float l = LineLength;

        // 设置四个角的 L 型坐标
        Vector3[] p0 = { new Vector3(-s, s - l), new Vector3(-s, s), new Vector3(-s + l, s) }; // 左上
        Vector3[] p1 = { new Vector3(s - l, s), new Vector3(s, s), new Vector3(s, s - l) };    // 右上
        Vector3[] p2 = { new Vector3(s, -s + l), new Vector3(s, -s), new Vector3(s - l, -s) }; // 右下
        Vector3[] p3 = { new Vector3(-s + l, -s), new Vector3(-s, -s), new Vector3(-s, -s + l) }; // 左下

        lines[0].SetPositions(p0); lines[1].SetPositions(p1);
        lines[2].SetPositions(p2); lines[3].SetPositions(p3);
    }
}