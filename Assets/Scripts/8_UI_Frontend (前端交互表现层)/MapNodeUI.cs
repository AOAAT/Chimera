using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("=== UI 组件引用 ===")]
    public Image NodeIcon;
    public Image OutlineImage;
    public Button ClickArea;

    [Header("=== 视觉配置 ===")]
    public Color LockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color SelectableColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color PassedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private MapNodeData myData;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
        if (ClickArea != null) ClickArea.onClick.AddListener(OnNodeClicked);
    }

    public void Initialize(MapNodeData data, Sprite defaultIcon)
    {
        myData = data;
        gameObject.name = $"UI_{data.NodeID}";
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (myData == null) return;

        // --- 👇【加固：安全获取视觉引用】 ---
        MapVisualizer viz = FindObjectOfType<MapVisualizer>();
        if (viz == null) return;

        Sprite iconToDisplay = null;

        if (myData.NodeType == MapNodeType.Unknown && !myData.IsRevealed)
        {
            // 如果是问号房且未探明
            iconToDisplay = viz.GetIconForType(MapNodeType.Unknown);
        }
        else
        {
            // 否则显示真实的内核图标
            iconToDisplay = viz.GetIconForType(myData.HiddenRealType);
        }

        if (NodeIcon != null)
        {
            NodeIcon.sprite = iconToDisplay;
            // 如果没配图标，先隐藏 Image 组件防止显示白块
            NodeIcon.enabled = (iconToDisplay != null);
        }

        // --- 状态视觉处理 ---
        switch (myData.NodeState)
        {
            case MapNodeState.Locked:
                if (NodeIcon != null) NodeIcon.color = LockedColor;
                if (OutlineImage != null) OutlineImage.enabled = false;
                if (ClickArea != null) ClickArea.interactable = false;
                break;
            case MapNodeState.Selectable:
                if (NodeIcon != null) NodeIcon.color = SelectableColor;
                if (OutlineImage != null) { OutlineImage.enabled = true; OutlineImage.color = SelectableColor; }
                if (ClickArea != null) ClickArea.interactable = true;
                break;
            case MapNodeState.Passed:
                if (NodeIcon != null) NodeIcon.color = PassedColor;
                if (OutlineImage != null) { OutlineImage.enabled = true; OutlineImage.color = PassedColor; }
                if (ClickArea != null) ClickArea.interactable = false;
                break;
        }
    }

    private void OnNodeClicked()
    {
        if (MapManager.Instance != null)
        {
            // --- 👇【注入音效】---
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Map_NodeSelect);

            MapManager.Instance.TrySelectNode(myData.NodeID);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (myData != null && myData.NodeState == MapNodeState.Selectable)
            transform.localScale = originalScale * 1.15f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }
}