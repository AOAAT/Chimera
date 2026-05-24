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
        // 1. 只有按下右键才执行逻辑
        if (!Input.GetMouseButtonDown(1)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // --- 🌟 核心：清理上一个还存在的点击指示标 ---
        if (currentActiveMarker != null) Destroy(currentActiveMarker);

        // 记录本次点击是否下达了有效指令
        bool anyCommandIssued = false;

        // 2. 优先级 A：如果当前选中了组装厂，右键点击执行“设置集合点”
        if (CurrentSelectedBuilding != null && CurrentSelectedBuilding is AssemblerBuilding assembler)
        {
            assembler.SetRallyPoint(mousePos);
            anyCommandIssued = true;
            // 提示：集合点设置后不需要指示标一直存在，逻辑已由 AssemblerBuilding 的虚线接管
        }
        else
        {
            // 3. 优先级 B：如果选中了居民，下达移动指令
            if (SelectedResidents.Count > 0)
            {
                foreach (var res in SelectedResidents)
                {
                    if (res != null)
                    {
                        res.SetDestination(mousePos);
                        anyCommandIssued = true;
                    }
                }
            }

            // 4. 优先级 C：如果选中了机甲单位，下达移动或攻击指令
            if (SelectedUnits.Count > 0)
            {
                // 判定是否点击了敌人
                RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));

                foreach (var unit in SelectedUnits)
                {
                    if (unit == null) continue;

                    if (enemyHit.collider != null)
                    {
                        // 集火攻击指令
                        unit.SetManualTarget(enemyHit.collider.transform);
                        anyCommandIssued = true;
                    }
                    else
                    {
                        // 地面移动指令
                        unit.SetManualMovePoint(mousePos);
                        anyCommandIssued = true;
                    }
                }
            }
        }

        // --- 🌟 关键修复：只有在确实下达了指令时，才生成特效 ---
        if (anyCommandIssued && ClickVFXPrefab != null)
        {
            // 将 currentActiveMarker 存入寄存器，以便下次点击时销毁
            currentActiveMarker = Instantiate(ClickVFXPrefab, new Vector3(mousePos.x, mousePos.y, -0.5f), Quaternion.identity);
        }
    }

    // ==========================================
    // 🔍 选中逻辑 (处理单位与建筑的排他性)
    // ==========================================
    private void SingleSelect()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // 1. 尝试探测居民 (优先最高)
        Collider2D residentHit = Physics2D.OverlapCircle(mousePos, 0.2f, LayerMask.GetMask("Resident"));

        // 2. 尝试探测机甲 (Player_Body 层)
        Collider2D unitHit = Physics2D.OverlapCircle(mousePos, 0.3f, LayerMask.GetMask("Player_Body"));

        // 3. 尝试探测建筑 (Building 层)
        Collider2D buildingHit = Physics2D.OverlapCircle(mousePos, 0.2f, LayerMask.GetMask("Building"));

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();
        SelectedResidents.Clear(); // 👈 清理居民列表
        CurrentSelectedBuilding = null;

        if (residentHit != null)
        {
            var resident = residentHit.GetComponentInParent<ResidentEntity>();
            if (resident != null)
            {
                SelectedResidents.Add(resident);
                resident.SetSelected(true);
            }
        }
        // 逻辑：如果点中了单位，清除建筑选中；点中了建筑，清除单位选中
        else if (unitHit != null)
        {
            var unit = unitHit.GetComponentInParent<ChimeraAIController>();
            if (unit != null)
            {
                SelectedUnits.Add(unit);
                ApplySelectionVisuals(unit);
            }
        }
        else if (buildingHit != null)
        {
            BuildingBase building = buildingHit.GetComponentInParent<BuildingBase>();
            if (building != null)
            {
                CurrentSelectedBuilding = building;
                building.SetSelected(true);
                MainBuildingHUD.Instance.Refresh(building);
            }
        }
        else
        {
            CurrentSelectedBuilding = null;
            // --- 🌟 核心联动：清空底部面板 ---
            MainBuildingHUD.Instance.Refresh(null);
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
        SelectedResidents.Clear(); // 👈 清理
        CurrentSelectedBuilding = null;


        // 1. 框选机甲
        Collider2D[] unitHits = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Player_Body"));
        foreach (var hit in unitHits)
        {
            var unit = hit.GetComponentInParent<ChimeraAIController>();
            if (unit != null && !SelectedUnits.Contains(unit))
            {
                SelectedUnits.Add(unit);
                ApplySelectionVisuals(unit);
            }
        }

        // 2. 框选居民 (新增)
        Collider2D[] residentHits = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Resident"));
        foreach (var hit in residentHits)
        {
            var resident = hit.GetComponentInParent<ResidentEntity>();
            if (resident != null && !SelectedResidents.Contains(resident))
            {
                SelectedResidents.Add(resident);
                resident.SetSelected(true);
            }
        }
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