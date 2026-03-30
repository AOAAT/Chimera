using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemContextMenuUI : MonoBehaviour
{
    public static ItemContextMenuUI Instance;

    public RectTransform MenuRect;
    public Button UpgradeButton;
    public TMP_Text ErrorPromptText; // 失败时的飘字提示

    private InstancedComponent currentTarget;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        UpgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    // 接收右键点击，在鼠标位置展开菜单
    public void ShowMenu(InstancedComponent target, Vector2 screenPos)
    {
        currentTarget = target;
        gameObject.SetActive(true);
        ErrorPromptText.text = ""; // 清空之前的报错

        // 将菜单位置移动到鼠标点击处
        MenuRect.position = screenPos;
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
        currentTarget = null;
    }

    private void OnUpgradeClicked()
    {
        if (currentTarget == null) return;

        // 呼叫底层合成大脑，进行“库存检索与拦截”
        bool canUpgrade = ComponentUpgradeManager.Instance.TryInitiateUpgrade(currentTarget, out UpgradePreviewData previewData, out string errorMsg);

        if (canUpgrade)
        {
            // 检索成功！找到了祭品！打开双栏预览面板！
            UpgradePreviewPanelUI.Instance.OpenPreview(previewData);
            HideMenu();
        }
        else
        {
            // 拦截生效！(如：已满级、没有同级祭品)
            ErrorPromptText.text = $"<color=#FF0000>{errorMsg}</color>";
            Debug.LogWarning($"【强化驳回】{errorMsg}");
        }
    }

    // 点击屏幕其他地方时关闭菜单
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(MenuRect, Input.mousePosition))
        {
            HideMenu();
        }
    }
}