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

        // 建造开始：清空单位选中，防止干扰
        if (BattleCommandManager.Instance != null)
        {
            BattleCommandManager.Instance.SelectedUnits.Clear();
            BattleCommandManager.Instance.ForceClearSelectionBox();
        }

        currentPendingData = data;
        GameObject go = Instantiate(data.Prefab);
        ghostInstance = go.GetComponent<BuildingBase>();
        ghostInstance.InitGhostMode();

        isPlacing = true;
        Debug.Log("<color=#FF00FF>【建造】</color> 启动放置流程: " + data.BuildingName);
    }

    private void Update()
    {
        // 硬锁定处理：确保点击放置后，鼠标抬起前不会触发选中
        if (isSelectionLocked)
        {
            if (Input.GetMouseButtonUp(0)) StartCoroutine(UnlockRoutine());
            return;
        }

        if (!isPlacing || ghostInstance == null) return;

        // 1. 严格吸附
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        ghostInstance.SnapToGrid(mouseWorldPos);

        // 2. 洪水审计：自检 + 他检
        bool isValid = CheckPlacementValidity();
        ghostInstance.UpdateGhostVisual(isValid);

        // 3. 点击判定
        if (Input.GetMouseButtonDown(0))
        {
            if (isValid) ConfirmPlacement();
            else Debug.LogWarning("<color=red>【拦截】</color> 位置不合法或造成了路径围堵！");
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private bool CheckPlacementValidity()
    {
        var sys = RTSGridSystem.Instance;
        HashSet<Vector2Int> ghostFootprint = new HashSet<Vector2Int>();

        // 第一步：基础物理碰撞占位检查
        foreach (Vector2Int offset in ghostInstance.FootprintOffsets)
        {
            Vector3 worldPos = ghostInstance.transform.position + new Vector3(offset.x * sys.CellSize, offset.y * sys.CellSize, 0);
            Vector2Int gridIdx = sys.WorldToGrid(worldPos);
            GridCell cell = sys.GetCell(gridIdx.x, gridIdx.y);

            if (cell == null || cell.IsOccupied) return false;
            ghostFootprint.Add(gridIdx);
        }

        // 第二步：洪水算法分析连通性
        HashSet<Vector2Int> liveArea = ConnectivityManager.GetAccessibleArea(ghostFootprint);

        // A. 自检：幽灵建筑是否有路可走？
        bool selfCanExit = ghostInstance.InteractionOffsets.Any(offset => {
            Vector3 pos = ghostInstance.transform.position + new Vector3(offset.x * sys.CellSize, offset.y * sys.CellSize, 0);
            return liveArea.Contains(sys.WorldToGrid(pos));
        });
        if (!selfCanExit) return false;

        // B. 他检：场上已有建筑是否会被堵死？
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

    private void ConfirmPlacement()
    {
        // 验证期：资源无限
        ghostInstance.FinalizePlacement();
        isPlacing = false;
        isSelectionLocked = true; // 激活硬锁定

        ghostInstance = null;
        currentPendingData = null;
        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Attach);
    }

    private void CancelPlacement()
    {
        if (ghostInstance != null) Destroy(ghostInstance.gameObject);
        isPlacing = false;
        ghostInstance = null;
        if (BattleCommandManager.Instance != null) BattleCommandManager.Instance.ForceClearSelectionBox();
    }

    private IEnumerator UnlockRoutine()
    {
        yield return null; // 等待当前帧事件结束
        isSelectionLocked = false;
        Debug.Log("<color=green>【建造】</color> 选中系统已解锁");
    }
}