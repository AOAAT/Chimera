using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BattleCommandManager : MonoBehaviour
{
    public static BattleCommandManager Instance;

    [Header("=== RTS 选中寄存器 ===")]
    public List<ChimeraAIController> SelectedUnits = new List<ChimeraAIController>();
    public BuildingBase CurrentSelectedBuilding;

    [Header("=== 居民选中寄存器 ===")]
    public List<ResidentEntity> SelectedResidents = new List<ResidentEntity>();


    [Header("=== 视觉组件引用 ===")]
    public RectTransform SelectionBoxUI;
    public GameObject ClickVFXPrefab;

    private Vector2 dragStartMousePos;
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

        SelectedResidents.RemoveAll(res => res == null);
        // 1. UI 拦截
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 2. 🌟 建造锁拦截：如果 BuildingManager 正在忙，本脚本彻底进入静默
        if (BuildingManager.Instance != null && (BuildingManager.Instance.IsPlacing || BuildingManager.Instance.IsSelectionLocked))
        {
            ForceClearSelectionBox();
            return;
        }

        HandleMarqueeSelection();
        HandleCommand();
    }

    // 🌟 核心接口：供外部调用，强行让框选起点对齐当前鼠标
    public void SyncDragStartToMouse()
    {
        dragStartMousePos = Input.mousePosition;
    }
    private void HandleCommand()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (currentActiveMarker != null) Destroy(currentActiveMarker);

        // 探测所有潜在目标
        RaycastHit2D buildingHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Building"));
        RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));
        RaycastHit2D mechHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Player_Body"));

        bool anyCommandIssued = false;

        // ==========================================
        // 优先级 1：【居民指令】 (如果选中了居民)
        // ==========================================
        if (SelectedResidents.Count > 0)
        {
            IResidentCarrier carrier = null;
            // 尝试从建筑或机甲获取载体接口
            if (buildingHit.collider != null) carrier = buildingHit.collider.GetComponentInParent<IResidentCarrier>();
            if (carrier == null && mechHit.collider != null) carrier = mechHit.collider.GetComponentInParent<IResidentCarrier>();

            foreach (var res in SelectedResidents)
            {
                if (res == null) continue;
                if (carrier != null) res.OrderGarrison(carrier);
                else res.SetDestination(mousePos);
            }
            anyCommandIssued = true;
        }
        // ==========================================
        // 优先级 2：【机甲指令】 (如果选中了机甲)
        // ==========================================
        else if (SelectedUnits.Count > 0)
        {
            foreach (var unit in SelectedUnits)
            {
                if (unit == null) continue;
                if (enemyHit.collider != null) unit.SetManualTarget(enemyHit.collider.transform);
                else unit.SetManualMovePoint(mousePos);
            }
            anyCommandIssued = true;
        }
        // ==========================================
        // 优先级 3：【建筑集合点】 (仅当没选单位，且选了组装厂)
        // ==========================================
        else if (CurrentSelectedBuilding != null && CurrentSelectedBuilding is AssemblerBuilding assembler)
        {
            // 只有点击的是空地或建筑（不是敌人的时候），设置集合点
            if (enemyHit.collider == null)
            {
                assembler.SetRallyPoint(mousePos);
                anyCommandIssued = true;
                Debug.Log($"<color=cyan>[建筑]</color> {assembler.BuildingName} 集合点已更新至: {mousePos}");
                // 集合点不需要光圈特效，直接返回
                return;
            }
        }

        // 执行视觉反馈
        if (anyCommandIssued && ClickVFXPrefab != null)
        {
            currentActiveMarker = Instantiate(ClickVFXPrefab, new Vector3(mousePos.x, mousePos.y, -0.5f), Quaternion.identity);
        }
    }

    // ==========================================
    // 🔍 选中逻辑 (处理单位与建筑的排他性)
    // ==========================================
    private void SingleSelect()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // 探测层级
        Collider2D residentHit = Physics2D.OverlapCircle(mousePos, 0.2f, LayerMask.GetMask("Resident"));
        Collider2D unitHit = Physics2D.OverlapCircle(mousePos, 0.3f, LayerMask.GetMask("Player_Body"));
        Collider2D buildingHit = Physics2D.OverlapCircle(mousePos, 0.2f, LayerMask.GetMask("Building"));

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();
        SelectedResidents.Clear();

        // 🌟 [关键点]：首先清空当前选中的建筑引用
        CurrentSelectedBuilding = null;

        if (residentHit != null)
        {
            var res = residentHit.GetComponentInParent<ResidentEntity>();
            if (res != null)
            {
                SelectedResidents.Add(res);
                res.SetSelected(true);

                // 🌟 [关键]：通知 HUD 刷新，并传入 ResidentEntity 实例
                SelectionContextHUD.Instance.Refresh(res);
            }
        }
        else if (unitHit != null)
        {
            var mech = unitHit.GetComponentInParent<MechUnit2D>();
            if (mech != null)
            {
                var ai = mech.GetComponent<ChimeraAIController>();
                if (ai != null) SelectedUnits.Add(ai);
                ApplySelectionVisuals(ai);
                SelectionContextHUD.Instance.Refresh(mech);
            }
        }
        else if (buildingHit != null)
        {
            var building = buildingHit.GetComponentInParent<BuildingBase>();
            if (building != null)
            {
                // 🌟 [修复点 1]：必须赋值给指挥官的寄存器，右键逻辑才能生效！
                CurrentSelectedBuilding = building;

                building.SetSelected(true);
                SelectionContextHUD.Instance.Refresh(building);
            }
        }
        else
        {
            SelectionContextHUD.Instance.Refresh(null);
        }
    }
    private void HandleMarqueeSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragStartMousePos = Input.mousePosition;
            // Debug.Log("[Marquee] 记录起点: " + dragStartMousePos);
        }

        if (Input.GetMouseButton(0))
        {
            UpdateSelectionBoxUI();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (SelectionBoxUI.gameObject.activeSelf)
            {
                Debug.Log("[Marquee] 框选释放，执行结算");
                SelectUnitsInRect();
                SelectionBoxUI.gameObject.SetActive(false);
            }
            else
            {
                Debug.Log("[Marquee] 单击释放，执行选中");
                SingleSelect();
            }
        }
    }

    private void SelectUnitsInRect()
    {
        Vector3 p1 = Camera.main.ScreenToWorldPoint(dragStartMousePos);
        Vector3 p2 = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 min = Vector2.Min(p1, p2);
        Vector2 max = Vector2.Max(p1, p2);

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();
        SelectedResidents.Clear();

        // 1. 捞取机甲
        Collider2D[] unitHits = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Player_Body"));
        foreach (var hit in unitHits)
        {
            var ai = hit.GetComponentInParent<ChimeraAIController>();
            if (ai != null && !SelectedUnits.Contains(ai))
            {
                SelectedUnits.Add(ai);
                ApplySelectionVisuals(ai);
            }
        }

        // 2. 捞取居民
        Collider2D[] resHits = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Resident"));
        foreach (var hit in resHits)
        {
            var res = hit.GetComponentInParent<ResidentEntity>();
            if (res != null && !SelectedResidents.Contains(res))
            {
                SelectedResidents.Add(res);
                res.SetSelected(true);
            }
        }

        // 框选时，如果框住了东西，底部看板保持清空（除非你未来设计多选面板）
        SelectionContextHUD.Instance.Refresh(null);
    }
    // --- 辅助视觉逻辑 ---

    private void UpdateSelectionBoxUI()
    {
        Vector2 currentMousePos = Input.mousePosition;
        float distance = Vector2.Distance(dragStartMousePos, currentMousePos);

        if (distance > 10f)
        {
            if (!SelectionBoxUI.gameObject.activeSelf) SelectionBoxUI.gameObject.SetActive(true);

            // 映射 UI 坐标
            RectTransform parentRect = SelectionBoxUI.parent.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, dragStartMousePos, null, out Vector2 localStart);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, currentMousePos, null, out Vector2 localEnd);
            Vector2 min = Vector2.Min(localStart, localEnd);
            Vector2 max = Vector2.Max(localStart, localEnd);
            SelectionBoxUI.anchoredPosition = min;
            SelectionBoxUI.sizeDelta = max - min;
        }
    }

    private void ClearAllSelectionVisuals()
    {
        // 1. 清理机甲视觉
        foreach (var m in FindObjectsOfType<ChimeraAIController>())
        {
            m.GetComponentInChildren<TacticalBracket>(true)?.Hide();
            m.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);
        }
        // 2. 清理居民视觉 (新增)
        foreach (var r in FindObjectsOfType<ResidentEntity>())
        {
            r.SetSelected(false);
        }
        // 3. 清理建筑视觉
        foreach (var b in FindObjectsOfType<BuildingBase>())
        {
            b.SetSelected(false);
        }
    }

    private void ApplySelectionVisuals(ChimeraAIController unit)
    {
        unit.GetComponentInChildren<TacticalBracket>(true)?.Show(unit.HasManualTarget());
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

    public void ForceClearSelectionBox()
    {
        if (SelectionBoxUI != null && SelectionBoxUI.gameObject.activeSelf)
        {
            SelectionBoxUI.gameObject.SetActive(false);
        }
        // 只要被拦截，起点就必须跟着鼠标，防止残留位移
        SyncDragStartToMouse();
    }

}