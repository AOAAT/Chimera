using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class ProductionTaskDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform myRect;
    private CanvasGroup canvasGroup;
    private RectTransform containerRect;

    // 🌟 核心：记录点击时的局部偏移，防止“瞬移”
    private Vector2 pointerOffset;

    private void Awake()
    {
        // 自动向上寻找条目根节点
        ProductionTaskUIItem rootItem = GetComponentInParent<ProductionTaskUIItem>();
        if (rootItem != null)
        {
            myRect = rootItem.GetComponent<RectTransform>();
            canvasGroup = myRect.GetComponent<CanvasGroup>() ?? myRect.gameObject.AddComponent<CanvasGroup>();
            containerRect = myRect.parent as RectTransform;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (myRect == null || containerRect == null) return;

        // 1. 计算鼠标点击点相对于 UI 中心点的偏移量
        RectTransformUtility.ScreenPointToLocalPointInRectangle(myRect, eventData.position, eventData.pressEventCamera, out pointerOffset);

        // 2. 视觉反馈：半透明
        canvasGroup.alpha = 0.7f;
        canvasGroup.blocksRaycasts = false; // 🌟 关键：允许射线穿透自己，否则无法探测下方的兄弟
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (myRect == null || containerRect == null) return;

        // 3. 将屏幕坐标转为父容器内的局部坐标，实现平滑跟随
        Vector2 localCursor;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRect, eventData.position, eventData.pressEventCamera, out localCursor))
        {
            // 考虑点击时的偏移，防止跳变
            // myRect.localPosition = localCursor - pointerOffset; // 暂不手动改位置，靠 SiblingIndex 驱动
        }

        // 🌟 4. 核心：基于鼠标 Y 轴的实时“挤位”逻辑
        float mouseY = eventData.position.y;
        int currentIdx = myRect.GetSiblingIndex();

        for (int i = 0; i < containerRect.childCount; i++)
        {
            if (i == currentIdx) continue;

            RectTransform sibling = containerRect.GetChild(i) as RectTransform;
            if (sibling == null) continue;

            // 获取兄弟物体的世界坐标
            Vector3[] corners = new Vector3[4];
            sibling.GetWorldCorners(corners);
            float siblingCenterY = (corners[1].y + corners[0].y) / 2f;

            // 如果鼠标越过了兄弟的中点线，立即交换位置
            if (currentIdx < i && mouseY < siblingCenterY)
            {
                myRect.SetSiblingIndex(i);
                break;
            }
            else if (currentIdx > i && mouseY > siblingCenterY)
            {
                myRect.SetSiblingIndex(i);
                break;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (myRect == null) return;

        // 5. 还原状态
        canvasGroup.alpha = 1.0f;
        canvasGroup.blocksRaycasts = true;

        // 6. 同步后端数据顺序
        SyncDataOrder();
    }

    private void SyncDataOrder()
    {
        FactoryBuilding factory = SelectionContextHUD.Instance.CurrentTargetBuilding as FactoryBuilding;
        if (factory == null || containerRect == null) return;

        List<ProductionTask> newList = new List<ProductionTask>();
        foreach (Transform child in containerRect)
        {
            ProductionTaskUIItem item = child.GetComponent<ProductionTaskUIItem>();
            if (item != null && item.BindedTask != null)
            {
                newList.Add(item.BindedTask);
            }
        }

        factory.TaskQueue = newList;
        factory.SyncOrderFlag = true;
        Debug.Log("<color=cyan>【重排成功】</color> 后台任务队列已刷新。");
    }
}