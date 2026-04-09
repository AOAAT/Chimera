// --- START OF FILE UpgradePreviewPanelUI.cs ---
using UnityEngine;
using UnityEngine.UI;

public class UpgradePreviewPanelUI : MonoBehaviour
{
    public static UpgradePreviewPanelUI Instance;

    [Header("=== 双栏 UI 容器 ===")]
    [Tooltip("直接把做好的 ItemDetailPanelUI 预制体拖两个进来当子节点！")]
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

        // ==========================================
        // 极简复用魔法：把实体塞给现成的面板，一切自动搞定！
        // ==========================================

        // 1. 渲染左侧：当前状态
        LeftCard_Current.ShowComponentDetail(previewData.TargetItem);

        // 2. 渲染右侧：下一级状态
        // 我们直接凭空“捏造”一个高一星级的临时实体，喂给右侧面板！
        // 它的背景图、数值、特殊机制全部会自动走 ItemDetailPanelUI 的原有逻辑刷新！
        InstancedComponent nextLevelMock = new InstancedComponent(
            previewData.TargetItem.BaseData,
            previewData.NextLevel
        );
        RightCard_Next.ShowComponentDetail(nextLevelMock);
    }

    private void OnConfirmClicked()
    {
        if (currentPreviewData != null)
        {
            // 呼叫核心大脑执行吞噬升级！
            ComponentUpgradeManager.Instance.ConfirmAndExecuteUpgrade(currentPreviewData);
        }
        ClosePanel();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        currentPreviewData = null;
    }
}