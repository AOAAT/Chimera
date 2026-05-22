using UnityEngine;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;
    public bool IsPlacing => isPlacing;
    private BuildingDataSO currentPendingData;
    private BuildingBase ghostInstance;
    private bool isPlacing = false;
    private bool placementHappenedThisFrame = false;
    public bool PlacementHappened => placementHappenedThisFrame;
    private void Awake() => Instance = this;

    // 🌟 由 UI 调用：开始建造流程
    public void StartPlacement(BuildingDataSO data)
    {
        if (isPlacing) CancelPlacement();

        currentPendingData = data;
        GameObject go = Instantiate(data.Prefab);
        ghostInstance = go.GetComponent<BuildingBase>();
        ghostInstance.InitGhostMode();

        isPlacing = true;
    }

    private void Update()
    {
        if (!isPlacing || ghostInstance == null) return;

        // --- 1. 严格网格吸附 ---
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        ghostInstance.SnapToGrid(mouseWorldPos); // 调用 BuildingBase 里的吸附算法

        // --- 2. 合法性检查 ---
        bool isValid = CheckPlacementValidity();
        ghostInstance.UpdateGhostVisual(isValid);

        // --- 3. 交互 ---
        if (Input.GetMouseButtonDown(0) && isValid)
        {
            ConfirmPlacement();
        }
        else if (Input.GetMouseButtonDown(1)) // 右键取消
        {
            CancelPlacement();
        }
    }

    private bool CheckPlacementValidity()
    {
        foreach (Vector2Int offset in ghostInstance.FootprintOffsets)
        {
            // 计算每个格子在全球网格中的索引
            Vector3 cellWorldPos = ghostInstance.transform.position + new Vector3(offset.x, offset.y, 0);
            Vector2Int gridIdx = RTSGridSystem.Instance.WorldToGrid(cellWorldPos);

            GridCell cell = RTSGridSystem.Instance.GetCell(gridIdx.x, gridIdx.y);

            // 判定：如果格子越界、已被占用、或不可行走，则非法
            if (cell == null || cell.IsOccupied) return false;
        }
        return true;
    }

    private void ConfirmPlacement()
    {
        if (ConsumeResources(currentPendingData))
        {
            ghostInstance.FinalizePlacement();

            // 🌟 核心：标记这一帧已经用来放建筑了，别再点别的了
            placementHappenedThisFrame = true;

            isPlacing = false;
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
    }

    private bool ConsumeResources(BuildingDataSO data)
    {
        // TODO: 对接 GlobalResourceManager
        return true;
    }

    private void LateUpdate()
    {
        if (placementHappenedThisFrame)
        {
            placementHappenedThisFrame = false;
        }
    }
}