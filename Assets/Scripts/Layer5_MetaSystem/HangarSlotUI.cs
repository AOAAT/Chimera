using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class HangarSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("=== 状态切换节点 ===")]
    public GameObject EmptyStateObj;
    public GameObject OccupiedStateObj;

    [Header("=== 占用态 UI 绑定 ===")]
    public RectTransform UnitVisualContainer;
    public TMP_Text UnitNameText;
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text PowerText;

    [Header("=== 视觉缩放与排版控制 ===")]
    [Range(0.1f, 3f)]
    public float PreviewScale = 1.0f;
    public float WorldToUIMultiplier = 100f;

    [Header("=== 战场部署设置 ===")]
    [Tooltip("把挂载了 MechUnit2D 脚本的 2D 机甲预制体拖到这里")]
    public GameObject MechPrefab;

    private SavedUnitProfile bindedProfile;
    public int mySlotIndex = -1;
    public GameObject DeployedStampObj;

    private GameObject dragGhost;
    private RectTransform ghostRect;
    private Canvas rootCanvas;

    // ==========================================
    // 刷新格子显示 
    // ==========================================
    public void RefreshSlot(int index, SavedUnitProfile profile)
    {
        mySlotIndex = index;
        bindedProfile = profile;

        if (profile == null || profile.ChassisData == null)
        {
            EmptyStateObj.SetActive(true);
            OccupiedStateObj.SetActive(false);
            if (DeployedStampObj != null) DeployedStampObj.SetActive(false);
        }
        else
        {
            EmptyStateObj.SetActive(false);
            OccupiedStateObj.SetActive(true);

            float maxHP = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedHP);
            foreach (string compID in profile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null && comp.BaseData != null)
                    maxHP += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.AddedHP);
            }

            UnitNameText.text = profile.UnitName;
            HPText.text = $"HP: {profile.CurrentHP} / {maxHP}";
            APText.text = $"AP: {profile.CurrentAP}";
            PowerText.text = $"耗电: {CalculateTotalPowerCost(profile)}";

            BuildUnitVisual(profile);

            bool isDeployed = profile.IsDeployed;
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = isDeployed ? 0.5f : 1.0f;

            if (DeployedStampObj != null) DeployedStampObj.SetActive(isDeployed);
        }
    }

    private float CalculateTotalPowerCost(SavedUnitProfile profile)
    {
        if (profile == null || profile.ChassisData == null) return 0f;

        float power = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);

        if (PlayerInventoryManager.Instance == null) return power;

        foreach (string compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                power += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.PowerCost);
            }
        }
        return power;
    }

    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        foreach (Transform child in UnitVisualContainer)
        {
            Destroy(child.gameObject);
        }

        UnitVisualContainer.localScale = Vector3.one * PreviewScale;

        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(UnitVisualContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();

        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            string compID = profile.EquippedComponentIDs[i];

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];

            GameObject slotObj = new GameObject($"UI_Slot_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("UI_Hinge");
            hingeObj.transform.SetParent(slotRect, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Sprite_Visual");
            visObj.transform.SetParent(hingeObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();

            compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;
        }
    }

    public void OnSlotClicked()
    {
        if (bindedProfile != null && bindedProfile.IsDeployed)
        {
            Debug.LogWarning("【系统提示】该机甲正在战场执行任务，无法改装！");
            return;
        }

        bool isEmptySlot = (bindedProfile == null || bindedProfile.ChassisData == null);

        if (isEmptySlot)
        {
            Debug.Log($"【机库流转】{mySlotIndex} 号车位为空，已引导长官前往【组装车间】！");
            HangarMenuUI.Instance.TriggerCreateNewUnit(mySlotIndex);
        }
        else
        {
            Debug.Log($"【机库流转】{mySlotIndex} 号车位已停放机甲，正在打开【详情档案】！");
            HangarMenuUI.Instance.TriggerOpenUnitDetail(mySlotIndex, bindedProfile);
        }
    }

    // ==========================================
    // 拖拽系统核心 (已彻底重构净化)
    // ==========================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 只要有车，且不是部署状态，且是左键点击，就可以拖拽（方便换位）
        if (bindedProfile == null || bindedProfile.ChassisData == null || bindedProfile.IsDeployed) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

        dragGhost = new GameObject("UI_DragGhost");
        dragGhost.transform.SetParent(rootCanvas.transform, false);
        dragGhost.transform.SetAsLastSibling();
        ghostRect = dragGhost.AddComponent<RectTransform>();

        if (UnitVisualContainer.childCount > 0)
        {
            GameObject visualClone = Instantiate(UnitVisualContainer.GetChild(0).gameObject, dragGhost.transform);
            visualClone.transform.localPosition = Vector3.zero;
            visualClone.transform.localScale = Vector3.one * PreviewScale;
        }

        CanvasGroup group = dragGhost.AddComponent<CanvasGroup>();
        group.alpha = 0.6f;
        group.blocksRaycasts = false; // 必须关掉射线阻挡，否则松手时检测不到底下的格子

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            UpdateGhostPosition(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null) Destroy(dragGhost);
        if (bindedProfile == null || bindedProfile.ChassisData == null || bindedProfile.IsDeployed) return;

        // ==========================================
        // 逻辑 A：UI 内部换位 (扫描鼠标下方的其他 UI 格子)
        // ==========================================
        GameObject droppedObj = eventData.pointerCurrentRaycast.gameObject;
        if (droppedObj != null)
        {
            HangarSlotUI targetSlot = droppedObj.GetComponentInParent<HangarSlotUI>();
            if (targetSlot != null && targetSlot != this)
            {
                int sourceIndex = this.mySlotIndex;
                int targetIndex = targetSlot.mySlotIndex;
                var inventory = PlayerInventoryManager.Instance.HangarUnits;

                if (inventory[targetIndex] != null && inventory[targetIndex].IsDeployed)
                {
                    Debug.LogWarning("【防撞预警】目标车位上的机甲正在前线交战，无法挪车！");
                    return;
                }

                SavedUnitProfile temp = inventory[sourceIndex];
                inventory[sourceIndex] = inventory[targetIndex];
                inventory[targetIndex] = temp;

                Debug.Log($"【换位成功】长官，{sourceIndex} 号车位与 {targetIndex} 号车位的资产已互换！");
                HangarMenuUI.Instance.RefreshHangar();
                return; // 换位成功，直接结束
            }
        }

        // ==========================================
        // 逻辑 B：向世界空投 (判断总监状态与物理红绿区)
        // ==========================================
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase)
        {
            Debug.LogWarning("【部署拒绝】当前不在战前部署阶段！只能在机库内调整机甲位置！");
            return;
        }

        if (MechPrefab == null)
        {
            Debug.LogError("【防呆警告】长官！你还没有在 HangarSlotUI 预制体里挂载 MechPrefab！");
            return;
        }

        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(eventData.position);
        Vector2 dropPos2D = new Vector2(worldPoint.x, worldPoint.y);

        // 1. 气泡红区检测
        int noDeployLayerMask = LayerMask.GetMask("NoDeploy");
        Collider2D forbiddenHit = Physics2D.OverlapCircle(dropPos2D, 0.5f, noDeployLayerMask);
        if (forbiddenHit != null)
        {
            Debug.LogWarning("【空投驳回】指挥官！该体积范围内有敌人禁区，强制禁止部署！");
            return;
        }

        // 2. 绿区合法性检测
        Collider2D[] allHits = Physics2D.OverlapPointAll(dropPos2D);
        bool isValidDeployZone = false;
        foreach (var hit in allHits)
        {
            if (hit.CompareTag("DeployZone"))
            {
                isValidDeployZone = true;
                break;
            }
        }

        // 3. 执行下兵
        if (isValidDeployZone)
        {
            bindedProfile.IsDeployed = true;
            Vector3 spawnPos = new Vector3(dropPos2D.x, dropPos2D.y, 0f);
            GameObject newMech = Instantiate(MechPrefab, spawnPos, Quaternion.identity);

            MechUnit2D mechScript = newMech.GetComponent<MechUnit2D>();
            if (mechScript != null)
            {
                mechScript.InitUnitData(bindedProfile);
            }

            Debug.Log($"【天降正义】[{bindedProfile.UnitName}] 已成功部署到战场坐标: {spawnPos}");
            HangarMenuUI.Instance.RefreshHangar();
        }
        else
        {
            Debug.LogWarning("【空投失败】长官，该坐标没有铺设 DeployZone 地板，或者扔到了虚空里！");
        }
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPointerPosition);
        ghostRect.localPosition = localPointerPosition;
    }
}