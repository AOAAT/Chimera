using UnityEngine;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;
    public bool IsPlacing => isPlacing;
    private BuildingDataSO currentPendingData;
    private BuildingBase ghostInstance;
    private bool isPlacing = false;
    private bool isSelectionLocked = false;
    public bool IsSelectionLocked => isSelectionLocked;

    private void Awake() => Instance = this;

    public void StartPlacement(BuildingDataSO data)
    {
        if (isPlacing) CancelPlacement();

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
        Debug.Log("<color=#FF00FF>[Building-System]</color> 进入建造模式");
    }

    private void Update()
    {
        // 🌟 核心加固：处理硬锁定解除
        if (isSelectionLocked)
        {
            if (Input.GetMouseButtonUp(0))
            {
                // 延迟到下一帧解锁，或者在当前帧保持锁定直到 LateUpdate
                StartCoroutine(UnlockSelectionNextFrame());
                Debug.Log("<color=#00FF00>[Building-System]</color> 探测到鼠标抬起，请求解锁...");
            }
            return;
        }

        if (!isPlacing || ghostInstance == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        ghostInstance.SnapToGrid(mouseWorldPos);

        bool isValid = CheckPlacementValidity();
        ghostInstance.UpdateGhostVisual(isValid);

        // 交互
        if (Input.GetMouseButtonDown(0))
        {
            if (isValid)
            {
                Debug.Log("<color=#00FFFF>[Building-System]</color> 点击左键：位置合法，开始放置");
                ConfirmPlacement();
            }
            else
            {
                Debug.LogWarning("<color=red>[Building-System]</color> 点击左键：位置非法，拦截放置");
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private System.Collections.IEnumerator UnlockSelectionNextFrame()
    {
        // 🌟 确保在本帧剩余的所有 Update 逻辑中，锁定依然生效
        yield return null;
        isSelectionLocked = false;
        Debug.Log("<color=#00FF00>[Building-System]</color> 锁定已彻底释放");
    }

    private void ConfirmPlacement()
    {
        if (ConsumeResources(currentPendingData))
        {
            ghostInstance.FinalizePlacement();
            isPlacing = false;
            isSelectionLocked = true; // 开启硬锁定

            ghostInstance = null;
            currentPendingData = null;
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Attach);
        }
    }

    private void CancelPlacement()
    {
        if (ghostInstance != null) Destroy(ghostInstance.gameObject);
        isPlacing = false;
        ghostInstance = null;
        if (BattleCommandManager.Instance != null) BattleCommandManager.Instance.ForceClearSelectionBox();
        Debug.Log("<color=orange>[Building-System]</color> 取消建造");
    }

    private bool CheckPlacementValidity()
    {
        if (ghostInstance == null) return false;
        foreach (Vector2Int offset in ghostInstance.FootprintOffsets)
        {
            Vector3 cellWorldPos = ghostInstance.transform.position + new Vector3(offset.x * RTSGridSystem.Instance.CellSize, offset.y * RTSGridSystem.Instance.CellSize, 0);
            Vector2Int gridIdx = RTSGridSystem.Instance.WorldToGrid(cellWorldPos);
            GridCell cell = RTSGridSystem.Instance.GetCell(gridIdx.x, gridIdx.y);
            if (cell == null || cell.IsOccupied) return false;
        }
        return true;
    }

    private bool ConsumeResources(BuildingDataSO data) => true;
}