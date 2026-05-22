using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BattleCommandManager : MonoBehaviour
{
    public static BattleCommandManager Instance;

    [Header("=== RTS 选中寄存器 ===")]
    public List<ChimeraAIController> SelectedUnits = new List<ChimeraAIController>();
    public BuildingBase CurrentSelectedBuilding;

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
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 🌟 核心加固：多重防护拦截
        if (BuildingManager.Instance != null)
        {
            bool isPlacing = BuildingManager.Instance.IsPlacing;
            bool isLocked = BuildingManager.Instance.IsSelectionLocked;

            if (isPlacing || isLocked)
            {
                // 1. 强制同步锚点
                dragStartMousePos = Input.mousePosition;

                // 2. 强制关闭视觉
                if (SelectionBoxUI.gameObject.activeSelf)
                {
                    Debug.Log("<color=red>[Command-Guard]</color> 拦截到建造模式下的框选尝试，已关闭视觉");
                    SelectionBoxUI.gameObject.SetActive(false);
                }

                // 3. 诊断 Log (如果此时还出现点击，查看是哪种状态)
                if (Input.GetMouseButtonUp(0))
                {
                    Debug.Log($"<color=gray>[Command-Guard]</color> 拦截到 MouseUp。状态: Placing={isPlacing}, Locked={isLocked}");
                }

                return; // 彻底切断
            }
        }

        HandleMarqueeSelection();
        HandleCommand();
        UpdateTargetingLine();
    }
    // ==========================================
    // ⚔️ 指令控制中枢 (已修复重复定义与逻辑冲突)
    // ==========================================
    private void HandleCommand()
    {
        // 1. 只有按下右键才执行
        if (!Input.GetMouseButtonDown(1)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // 2. 优先级 A：如果当前选中了组装厂，右键点击执行“设置集合点”
        if (CurrentSelectedBuilding != null && CurrentSelectedBuilding is AssemblerBuilding assembler)
        {
            assembler.SetRallyPoint(mousePos);
            return; // 指令已处理，直接返回
        }

        // 3. 优先级 B：如果选中了机甲单位，右键点击执行“移动/攻击”指令
        if (SelectedUnits.Count > 0)
        {
            // 判定是否点击了敌人
            RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));

            if (currentActiveMarker != null) Destroy(currentActiveMarker);

            foreach (var unit in SelectedUnits)
            {
                if (unit == null) continue;

                if (enemyHit.collider != null)
                {
                    // 集火攻击指令
                    unit.SetManualTarget(enemyHit.collider.transform);
                }
                else
                {
                    // 地面移动指令
                    unit.SetManualMovePoint(mousePos);

                    // 只在第一个单位的位置生成点击特效
                    if (unit == SelectedUnits[0] && ClickVFXPrefab != null)
                    {
                        currentActiveMarker = Instantiate(ClickVFXPrefab, new Vector3(mousePos.x, mousePos.y, -0.5f), Quaternion.identity);
                    }
                }
            }
        }
    }

    // ==========================================
    // 🔍 选中逻辑 (处理单位与建筑的排他性)
    // ==========================================
    private void SingleSelect()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // A. 尝试探测机甲 (Player_Body 层)
        Collider2D unitHit = Physics2D.OverlapCircle(mousePos, 0.3f, LayerMask.GetMask("Player_Body"));

        // B. 尝试探测建筑 (Building 层)
        Collider2D buildingHit = Physics2D.OverlapCircle(mousePos, 0.2f, LayerMask.GetMask("Building"));

        ClearAllSelectionVisuals();
        SelectedUnits.Clear();

        // 逻辑：如果点中了单位，清除建筑选中；点中了建筑，清除单位选中
        if (unitHit != null)
        {
            CurrentSelectedBuilding = null; // 清除建筑选中
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

                // --- 🌟 核心联动：刷新底部面板 ---
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
        CurrentSelectedBuilding = null; // 框选操作强制取消建筑选中

        Collider2D[] hits = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Player_Body"));
        foreach (var hit in hits)
        {
            var unit = hit.GetComponentInParent<ChimeraAIController>();
            if (unit != null && !SelectedUnits.Contains(unit))
            {
                SelectedUnits.Add(unit);
                ApplySelectionVisuals(unit);
            }
        }
    }

    // --- 辅助视觉逻辑 ---

    private void UpdateSelectionBoxUI()
    {
        if (BuildingManager.Instance != null && (BuildingManager.Instance.IsPlacing || BuildingManager.Instance.IsSelectionLocked))
        {
            SelectionBoxUI.gameObject.SetActive(false);
            return;
        }


        Vector2 currentMousePos = Input.mousePosition;
        float distance = Vector2.Distance(dragStartMousePos, currentMousePos);

        // 只有拖拽距离超过 10 像素才显示
        if (distance > 10f)
        {
            if (!SelectionBoxUI.gameObject.activeSelf)
            {
                // 🌟 这是最关键的触发点
                Debug.Log($"<color=yellow>[Marquee-Trigger]</color> 开启框选框视觉。当前帧: {Time.frameCount}");
                SelectionBoxUI.gameObject.SetActive(true);
            }

            // --- 坐标转换计算 (保持不变) ---
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
        // 清理机甲视觉
        foreach (var m in FindObjectsOfType<ChimeraAIController>())
        {
            m.GetComponentInChildren<TacticalBracket>(true)?.Hide();
            m.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);
        }
        // 清理建筑视觉
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
        if (SelectionBoxUI != null)
        {
            SelectionBoxUI.gameObject.SetActive(false);
        }
    }
}