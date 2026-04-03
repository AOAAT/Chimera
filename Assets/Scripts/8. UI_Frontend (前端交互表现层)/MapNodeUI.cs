using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("=== UI 组件引用 ===")]
    public Image NodeIcon;       // 中间的图标 (骷髅头/问号等)
    public Image OutlineImage;   // 外圈高亮/打勾的框
    public Button ClickArea;     // 按钮组件

    [Header("=== 视觉配置 ===")]
    public Color LockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color SelectableColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color PassedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private MapNodeData myData;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
        ClickArea.onClick.AddListener(OnNodeClicked);
    }

    // 接收上帝视角的注入
    public void Initialize(MapNodeData data, Sprite myIcon)
    {
        myData = data;
        gameObject.name = $"UI_{data.NodeID}";

        // 👇【核心替换】：把传进来的贴图赋给 Image 组件
        if (myIcon != null && NodeIcon != null)
        {
            NodeIcon.sprite = myIcon;
        }

        RefreshVisualState();
    }

    // 刷新颜色和表现
    public void RefreshVisualState()
    {
        if (myData == null) return;

        // 根据不同类型换图标 (这里您可以未来接您的 Sprite 库)
        // NodeIcon.sprite = ... 

        switch (myData.NodeState)
        {
            case MapNodeState.Locked:
                NodeIcon.color = LockedColor;
                OutlineImage.enabled = false;
                ClickArea.interactable = false;
                break;
            case MapNodeState.Selectable:
                NodeIcon.color = SelectableColor;
                OutlineImage.enabled = true;
                OutlineImage.color = SelectableColor; // 这里未来可以挂个 Animator 做呼吸动画
                ClickArea.interactable = true;
                break;
            case MapNodeState.Passed:
                NodeIcon.color = PassedColor;
                OutlineImage.enabled = true;
                OutlineImage.color = PassedColor; // 打个勾或者变暗
                ClickArea.interactable = false;
                break;
        }
    }

    private void OnNodeClicked()
    {
        // 呼叫全局管理器：玩家请求进入这个节点！
        MapManager.Instance.TrySelectNode(myData.NodeID);
    }

    // --- 悬停放大效果 (极佳的交互反馈) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (myData.NodeState == MapNodeState.Selectable)
            transform.localScale = originalScale * 1.2f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }
}