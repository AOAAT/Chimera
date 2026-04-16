// --- START OF FILE UpgradePreviewPanelUI.cs ---
using UnityEngine;
using UnityEngine.UI;

public class UpgradePreviewPanelUI : MonoBehaviour
{
    public static UpgradePreviewPanelUI Instance;

    [Header("=== 双栏 UI 容器 ===")]
    public ItemDetailPanelUI LeftCard_Current;
    public ItemDetailPanelUI RightCard_Next;

    [Header("=== 交互按钮 ===")]
    public Button ConfirmButton;
    public Button CancelButton;

    private UpgradePreviewData currentPreviewData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        ConfirmButton.onClick.AddListener(OnConfirmClicked);
        CancelButton.onClick.AddListener(ClosePanel);
    }

    public void OpenPreview(UpgradePreviewData previewData)
    {
        currentPreviewData = previewData;
        gameObject.SetActive(true);

        // 1. 渲染左侧：当前状态 (普通模式)
        LeftCard_Current.ShowComponentDetail(previewData.TargetItem);

        // 2. 渲染右侧：下一级状态 (带 Diff 高亮！)
        InstancedComponent nextLevelMock = new InstancedComponent(previewData.TargetItem.BaseData, previewData.NextLevel);

        // 👇【核心新增】：我们给 ShowComponentDetail 追加一个可选参数，把 diffData 传进去！
        RightCard_Next.ShowComponentDetail(nextLevelMock, previewData);
    }

    private void OnConfirmClicked()
    {
        if (currentPreviewData != null) ComponentUpgradeManager.Instance.ConfirmAndExecuteUpgrade(currentPreviewData);
        ClosePanel();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        currentPreviewData = null;
    }
}