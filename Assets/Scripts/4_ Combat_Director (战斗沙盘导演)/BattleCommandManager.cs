using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BattleCommandManager : MonoBehaviour
{
    public static BattleCommandManager Instance;

    [Header("=== RTS 选中序列 ===")]
    public List<ChimeraAIController> SelectedUnits = new List<ChimeraAIController>();

    [Header("=== 视觉组件引用 ===")]
    public RectTransform SelectionBoxUI;
    public GameObject ClickVFXPrefab;

    private Vector2 dragStartMousePos; // 记录鼠标点击的原始屏幕位置
    private LineRenderer targetingLine;
    private GameObject currentActiveMarker;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        targetingLine = gameObject.AddComponent<LineRenderer>();
        targetingLine.startWidth = targetingLine.endWidth = 0.04f;
        targetingLine.material = new Material(Shader.Find("Sprites/Default"));
        targetingLine.startColor = new Color(1, 0, 0, 0.7f);
        targetingLine.endColor = new Color(1, 0, 0, 0.1f);
        targetingLine.enabled = false;
        targetingLine.sortingLayerName = "UI";
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        HandleMarqueeSelection();
        HandleCommand();
        UpdateTargetingLine();
    }

    private void HandleMarqueeSelection()
    {
        // --- 1. 起点记录 ---
        if (Input.GetMouseButtonDown(0))
        {
            dragStartMousePos = Input.mousePosition;
        }

        // --- 2. 拖拽实时更新视觉 ---
        if (Input.GetMouseButton(0))
        {
            UpdateSelectionBoxUI();
        }

        // --- 3. 终点判定 ---
        if (Input.GetMouseButtonUp(0))
        {
            if (SelectionBoxUI.gameObject.activeSelf)
            {
                SelectUnitsInRect();
                SelectionBoxUI.gameObject.SetActive(false);
            }
            else
            {
                SingleSelect();
            }
        }
    }

    // 🚀【核心修正】：UI 框选框的坐标转换算法
    private void UpdateSelectionBoxUI()
    {
        Vector2 currentMousePos = Input.mousePosition;
        float distance = Vector2.Distance(dragStartMousePos, currentMousePos);

        // 只有拖拽距离超过一定阈值才显示框，防止点击时闪烁
        if (distance < 10f) return;

        if (!SelectionBoxUI.gameObject.activeSelf) SelectionBoxUI.gameObject.SetActive(true);

        // --- 坐标转换魔法 ---
        RectTransform parentRect = SelectionBoxUI.parent.GetComponent<RectTransform>();

        // 将屏幕起终点转换为 UI 容器内的局部坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, dragStartMousePos, null, out Vector2 localStart);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, currentMousePos, null, out Vector2 localEnd);

        // 计算 UI 矩形的左下角 (Min) 和 右上角 (Max)
        Vector2 min = Vector2.Min(localStart, localEnd);
        Vector2 max = Vector2.Max(localStart, localEnd);

        // 应用位置和大小
        // 因为 UI 框的 Pivot 建议设为 (0,0) [左下角] 或 (0.5, 0.5) [中心]
        // 这里我根据计算出的 min 位置设置 anchoredPosition
        SelectionBoxUI.anchoredPosition = min;
        SelectionBoxUI.sizeDelta = max - min;
    }

    private void SelectUnitsInRect()
    {
        // 1. 计算世界坐标系下的真实物理矩形
        Vector3 p1 = Camera.main.ScreenToWorldPoint(dragStartMousePos);
        Vector3 p2 = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2 min = Vector2.Min(p1, p2);
        Vector2 max = Vector2.Max(p1, p2);

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();

        // 2. 物理扫描
        int layerMask = LayerMask.GetMask("Player_Body", "Player_Hitbox");
        Collider2D[] hits = Physics2D.OverlapAreaAll(min, max, layerMask);

        Debug.Log($"<color=yellow>【框选透视】物理扫描区域: {min} 到 {max} | 捕获碰撞体数量: {hits.Length} 个</color>");

        // 3. 逐个筛选与鉴权
        foreach (var hit in hits)
        {
            var unit = hit.GetComponentInParent<ChimeraAIController>();

            // 嫌疑人 A：抓到了碰撞体，但它不是受控单位
            if (unit == null)
            {
                Debug.LogWarning($"<color=orange>【框选透视】抓到了 {hit.name}，但它身上(或父级)没有挂载 ChimeraAIController 脚本！</color>");
                continue;
            }

            if (!SelectedUnits.Contains(unit))
            {
                SelectedUnits.Add(unit);

                // 嫌疑人 B：是受控单位，但忘记挂载特效脚本
                TacticalBracket bracket = unit.GetComponentInChildren<TacticalBracket>(true);
                if (bracket == null)
                {
                    Debug.LogError($"<color=red>【框选透视】成功选中了 {unit.gameObject.name}，但它身上缺少 TacticalBracket 脚本，无法显示四角提示框！</color>");
                }

                ApplySelectionVisuals(unit);
            }
        }

        Debug.Log($"<color=green>【框选透视】最终成功编入小队的单位总数: {SelectedUnits.Count} 个</color>");
    }

    private void SingleSelect()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapCircle(mousePos, 0.3f, LayerMask.GetMask("Player_Body", "Player_Hitbox"));

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();

        if (hit != null)
        {
            var unit = hit.GetComponentInParent<ChimeraAIController>();
            if (unit != null)
            {
                SelectedUnits.Add(unit);
                ApplySelectionVisuals(unit);
            }
        }
    }

    // ==========================================
    // 其余逻辑（右键、视觉）保持不变，加固清理
    // ==========================================

    private void HandleCommand()
    {
        if (SelectedUnits.Count == 0 || !Input.GetMouseButtonDown(1)) return;
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));

        if (currentActiveMarker != null) Destroy(currentActiveMarker);

        foreach (var unit in SelectedUnits)
        {
            if (unit == null) continue;
            if (enemyHit.collider != null) unit.SetManualTarget(enemyHit.collider.transform);
            else
            {
                unit.SetManualMovePoint(mousePos);
                if (unit == SelectedUnits[0] && ClickVFXPrefab != null)
                {
                    currentActiveMarker = Instantiate(ClickVFXPrefab, new Vector3(mousePos.x, mousePos.y, -0.5f), Quaternion.identity);
                }
            }
        }
    }

    private void ClearAllSelectionVisuals()
    {
        ChimeraAIController[] all = FindObjectsOfType<ChimeraAIController>();
        foreach (var m in all)
        {
            m.GetComponentInChildren<TacticalBracket>(true)?.Hide();
            m.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);
        }
    }

    private void ApplySelectionVisuals(ChimeraAIController unit)
    {
        TacticalBracket bracket = unit.GetComponentInChildren<TacticalBracket>(true);
        if (bracket != null) bracket.Show(unit.HasManualTarget());
        unit.GetComponent<WeaponRangeVisualizer>()?.SetVisible(true);
    }

    private void UpdateTargetingLine()
    {
        if (SelectedUnits.Count > 0 && SelectedUnits[0] != null && SelectedUnits[0].HasManualTarget())
        {
            Transform t = SelectedUnits[0].GetManualTarget();
            if (t != null && t.gameObject.activeInHierarchy)
            {
                targetingLine.enabled = true;
                targetingLine.SetPosition(0, SelectedUnits[0].transform.position);
                targetingLine.SetPosition(1, t.position);
                return;
            }
        }
        targetingLine.enabled = false;
    }
}