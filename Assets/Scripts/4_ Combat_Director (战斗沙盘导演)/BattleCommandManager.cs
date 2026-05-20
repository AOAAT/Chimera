using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BattleCommandManager : MonoBehaviour
{
    public static BattleCommandManager Instance;

    [Header("=== RTS 选中序列 ===")]
    public List<ChimeraAIController> SelectedUnits = new List<ChimeraAIController>();

    [Header("=== 视觉反馈预制体 ===")]
    public GameObject ClickVFXPrefab;

    private LineRenderer targetingLine;
    private GameObject currentActiveMarker; // 追踪当前场上的唯一路径标识

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始化集火红线表现
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
        // 屏蔽点击 UI 菜单时的透传
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        HandleSelection(); // 左键逻辑
        HandleCommand();   // 右键逻辑
        UpdateTargetingLine();
    }

    private void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0)) // 左键点击
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int selectionMask = LayerMask.GetMask("Player_Body", "Player_Hitbox");
            Collider2D hit = Physics2D.OverlapCircle(mousePos, 0.25f, selectionMask);

            // 1. 熄灭全场所有机甲的选中视觉
            ClearAllSelectionVisuals();

            if (hit != null)
            {
                var unit = hit.GetComponentInParent<ChimeraAIController>();
                if (unit != null)
                {
                    // 2. 更新选中列表并激活视觉
                    SelectedUnits.Clear();
                    SelectedUnits.Add(unit);
                    ApplySelectionVisuals(unit);
                    Debug.Log($"<color=green>[Command-Log] 已选中机甲: {unit.name}. 当前选中总数: {SelectedUnits.Count}</color>");
                    return;
                }
            }
            // 点击空地清空选择
            SelectedUnits.Clear();
        }
    }

    private void HandleCommand()
    {
        // 没选中人或者没按右键，不执行
        if (SelectedUnits.Count == 0 || !Input.GetMouseButtonDown(1)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));

        // --- 核心修正：立即清理上一条指令留下的旧标识 ---
        if (currentActiveMarker != null)
        {
            Debug.Log("<color=yellow>[Command-Log] 覆盖指令：正在销毁旧的路点标识。</color>");
            Destroy(currentActiveMarker);
        }

        foreach (var unit in SelectedUnits)
        {
            if (unit == null) continue;

            if (enemyHit.collider != null)
            {
                // 指令：集火敌人
                unit.SetManualTarget(enemyHit.collider.transform);
            }
            else
            {
                // 指令：自由位移
                unit.SetManualMovePoint(mousePos);

                // 仅为第一个选中的单位生成路点，防止视觉污染
                if (unit == SelectedUnits[0] && ClickVFXPrefab != null)
                {
                    currentActiveMarker = Instantiate(ClickVFXPrefab, new Vector3(mousePos.x, mousePos.y, -0.5f), Quaternion.identity);
                    Debug.Log($"<color=cyan>[Command-Log] 已在坐标 {mousePos} 创建新路点。Z轴已校准为-0.5</color>");
                }
            }
        }
    }

    private void ClearAllSelectionVisuals()
    {
        // 寻找所有带大脑的单位并关闭框选
        ChimeraAIController[] all = FindObjectsOfType<ChimeraAIController>();
        foreach (var m in all)
        {
            m.GetComponentInChildren<TacticalBracket>(true)?.Hide();
            m.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);
        }
    }

    private void ApplySelectionVisuals(ChimeraAIController unit)
    {
        // 强制递归寻找支架组件并显示
        TacticalBracket b = unit.GetComponentInChildren<TacticalBracket>(true);
        if (b != null) b.Show(unit.HasManualTarget());

        WeaponRangeVisualizer r = unit.GetComponent<WeaponRangeVisualizer>();
        if (r != null) r.SetVisible(true);
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