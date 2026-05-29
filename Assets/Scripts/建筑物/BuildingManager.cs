using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    public bool IsPlacing => isPlacing;
    public bool IsSelectionLocked => isSelectionLocked;

    private BuildingDataSO currentPendingData;
    private BuildingBase ghostInstance;
    private bool isPlacing = false;
    private bool isSelectionLocked = false;

    private void Awake() => Instance = this;

    public void StartPlacement(BuildingDataSO data)
    {
        if (isPlacing) CancelPlacement();

        // 🌟 核心：进入建造模式瞬间，强制归零选中系统
        if (BattleCommandManager.Instance != null)
        {
            BattleCommandManager.Instance.SelectedUnits.Clear();
            BattleCommandManager.Instance.ForceClearSelectionBox();
            BattleCommandManager.Instance.SyncDragStartToMouse(); // 立即同步起点
        }

        currentPendingData = data;
        GameObject go = Instantiate(data.Prefab);
        ghostInstance = go.GetComponent<BuildingBase>();
        ghostInstance.InitGhostMode();

        isPlacing = true;
        if (CoverageVisualizer.Instance != null) CoverageVisualizer.Instance.SetVisible(true);
        Debug.Log("<color=#FF00FF>【系统】</color> 建造模式已锁定，选中系统已挂起。");
    }

    private void Update()
    {
        // 1. 硬锁定期间：完全拦截，直到鼠标完成一次 [Down-Up] 循环
        if (isSelectionLocked)
        {
            if (Input.GetMouseButtonUp(0))
            {
                StartCoroutine(UnlockRoutine());
            }
            // 锁定期间也得让选中系统的起点跟着走，防止残留位移
            if (BattleCommandManager.Instance != null) BattleCommandManager.Instance.SyncDragStartToMouse();
            return;
        }

        if (!isPlacing || ghostInstance == null) return;

        // 2. 严格吸附与合法性检测
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        ghostInstance.SnapToGrid(mouseWorldPos);

        bool isValid = CheckPlacementValidity();
        ghostInstance.UpdateGhostVisual(isValid);

        // 3. 建造交互
        if (Input.GetMouseButtonDown(0))
        {
            if (isValid) ConfirmPlacement();
            else Debug.LogWarning("<color=red>【拦截】</color> 此处无法建造");
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }

        // 🌟 极其重要：放置期间，选中系统的起点必须实时同步鼠标，防止产生跨图框选
        if (BattleCommandManager.Instance != null) BattleCommandManager.Instance.SyncDragStartToMouse();
    }

    private void ConfirmPlacement()
    {
        if (ghostInstance == null) return;
        ghostInstance.FinalizePlacement();

        isPlacing = false;
        isSelectionLocked = true; // 🌟 激活硬锁定

        ghostInstance = null;
        currentPendingData = null;
        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Attach);
        if (CoverageVisualizer.Instance != null) CoverageVisualizer.Instance.SetVisible(false);
        Debug.Log("<color=cyan>【系统】</color> 放置成功，等待鼠标抬起解锁。");
    }

    private IEnumerator UnlockRoutine()
    {
        // 延迟两帧，确保所有输入事件在当前帧彻底消耗干净
        yield return null;
        yield return null;
        isSelectionLocked = false;
        Debug.Log("<color=green>【系统】</color> 选中系统已安全解锁。");
    }

    public void CancelPlacement()
    {
        if (ghostInstance != null) Destroy(ghostInstance.gameObject);
        isPlacing = false;
        isSelectionLocked = false;
        ghostInstance = null;
        if (BattleCommandManager.Instance != null) BattleCommandManager.Instance.ForceClearSelectionBox();
        if (CoverageVisualizer.Instance != null) CoverageVisualizer.Instance.SetVisible(false);
    }

    private bool CheckPlacementValidity()
    {
        if (ghostInstance == null) return false;
        var sys = RTSGridSystem.Instance;
        HashSet<Vector2Int> ghostFootprint = new HashSet<Vector2Int>();

        foreach (Vector2Int offset in ghostInstance.FootprintOffsets)
        {
            Vector3 worldPos = ghostInstance.transform.position + new Vector3(offset.x * sys.CellSize, offset.y * sys.CellSize, 0);
            Vector2Int gridIdx = sys.WorldToGrid(worldPos);
            GridCell cell = sys.GetCell(gridIdx.x, gridIdx.y);
            if (cell == null || cell.IsOccupied) return false;
            ghostFootprint.Add(gridIdx);
        }

        // 连通性审计
        HashSet<Vector2Int> liveArea = ConnectivityManager.GetAccessibleArea(ghostFootprint);
        bool selfCanExit = ghostInstance.InteractionOffsets.Any(offset => {
            Vector3 pos = ghostInstance.transform.position + new Vector3(offset.x * sys.CellSize, offset.y * sys.CellSize, 0);
            return liveArea.Contains(sys.WorldToGrid(pos));
        });
        if (!selfCanExit) return false;

        foreach (var b in BuildingBase.AllPlacedBuildings)
        {
            bool bStillHasPath = b.InteractionOffsets.Any(offset => {
                Vector3 pos = b.transform.position + new Vector3(offset.x * sys.CellSize, offset.y * sys.CellSize, 0);
                return liveArea.Contains(sys.WorldToGrid(pos));
            });
            if (!bStillHasPath) return false;
        }
        return true;
    }
}