using UnityEngine;
using UnityEngine.UI;

public class HeadquartersBuilding : BuildingBase
{
    [Header("=== 招募设置 ===")]
    public float RecruitmentInterval = 20f; // 招募一名新人所需时间
    private float recruitTimer;

    [Header("=== UI 引用 ===")]
    public CanvasGroup ProgressCanvasGroup;
    public Image ProgressBarFill;


    protected override void Awake()
    {
        base.Awake();
        recruitTimer = RecruitmentInterval;
        isPlaced = true;
    }

    private void Update()
    {
        if (!isPlaced) return;

        // 只有人口没满时才运行计时器
        if (PopulationManager.Instance != null && !PopulationManager.Instance.IsFull())
        {
            HandleRecruitment();
            SetProgressUIActive(true);
        }
        else
        {
            SetProgressUIActive(false);
            recruitTimer = RecruitmentInterval; // 重置，等待下一次空位
        }
    }

    private void HandleRecruitment()
    {
        recruitTimer -= Time.deltaTime;

        // 更新进度条 (0 到 1)
        if (ProgressBarFill != null)
        {
            ProgressBarFill.fillAmount = 1f - (recruitTimer / RecruitmentInterval);
        }

        if (recruitTimer <= 0)
        {
            ExecuteSpawn();
            recruitTimer = RecruitmentInterval;
        }
    }

    private void ExecuteSpawn()
    {
        // 寻找第一个交互格作为出口
        Vector3 spawnPos = transform.position;
        if (InteractionOffsets != null && InteractionOffsets.Count > 0)
        {
            float cellSize = RTSGridSystem.Instance.CellSize;
            spawnPos = transform.position + new Vector3(InteractionOffsets[0].x * cellSize, InteractionOffsets[0].y * cellSize, 0);
        }

        PopulationManager.Instance.SpawnResidentAt(spawnPos);

        // 播放个轻微的音效
        if (GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ItemEject);
    }

    private void SetProgressUIActive(bool isActive)
    {
        if (ProgressCanvasGroup == null) return;
        ProgressCanvasGroup.alpha = Mathf.Lerp(ProgressCanvasGroup.alpha, isActive ? 1f : 0f, Time.deltaTime * 5f);
    }
}