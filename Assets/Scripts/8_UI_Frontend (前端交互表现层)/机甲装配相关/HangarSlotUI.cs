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

    // 👇【核心修复】：锁死底层换算率，统一暴露 PreviewScale
    [Header("=== 视觉缩放与排版控制 ===")]
    [Range(0.1f, 3f)]
    public float PreviewScale = 1.0f;
    private const float WorldToUIMultiplier = 100f; // 锁死

    [Header("=== 战场部署设置 ===")]
    public GameObject MechPrefab;

    private SavedUnitProfile bindedProfile;
    public int mySlotIndex = -1;
    public GameObject DeployedStampObj;

    private GameObject dragGhost;
    private RectTransform ghostRect;
    private Canvas rootCanvas;

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
                {
                    var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                    if (lvData != null) maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
                }
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
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) power += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
            }
        }
        return power;
    }

    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        foreach (Transform child in UnitVisualContainer) Destroy(child.gameObject);

        // PreviewScale 缩放！
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

        if (isEmptySlot) HangarMenuUI.Instance.TriggerCreateNewUnit(mySlotIndex);
        else HangarMenuUI.Instance.TriggerOpenUnitDetail(mySlotIndex, bindedProfile);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
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
        group.blocksRaycasts = false;

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null) UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null) Destroy(dragGhost);
        if (bindedProfile == null || bindedProfile.ChassisData == null || bindedProfile.IsDeployed) return;

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
                HangarMenuUI.Instance.RefreshHangar();
                return;
            }
        }

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase)
        {
            Debug.LogWarning("【部署拒绝】当前不在战前部署阶段！");
            return;
        }

        if (MechPrefab == null) return;

        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(eventData.position);
        Vector2 dropPos2D = new Vector2(worldPoint.x, worldPoint.y);

        int noDeployLayerMask = LayerMask.GetMask("NoDeploy");
        Collider2D forbiddenHit = Physics2D.OverlapCircle(dropPos2D, 0.5f, noDeployLayerMask);
        if (forbiddenHit != null) return;

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

        if (isValidDeployZone)
        {
            // 1. 算一下这台待空投的机甲要吃多少电
            int thisMechPower = GlobalResourceManager.Instance.CalculateUnitPowerCost(bindedProfile);

            // 2. 算一下战场上其他的机甲已经占了多少电
            int currentlyUsed = GlobalResourceManager.Instance.GetTotalUsedPower();

            // 3. 算总账：如果加上这台就爆闸了！
            if (currentlyUsed + thisMechPower > GlobalResourceManager.Instance.MaxPowerCapacity)
            {
                Debug.LogWarning($"【空投驳回】电网超载！这台机甲需要 {thisMechPower} 电量，剩余可用电量仅 {GlobalResourceManager.Instance.MaxPowerCapacity - currentlyUsed}！");

                // (未来这里可以接一个屏幕红字飘字的错误提示)
                return; // 直接拦截！跳出，机甲弹回原位！
            }

            // ==========================================
            // 安检通过，正式空投！
            // ==========================================
            bindedProfile.IsDeployed = true;
            Vector3 spawnPos = new Vector3(dropPos2D.x, dropPos2D.y, 0f);
            GameObject newMech = Instantiate(MechPrefab, spawnPos, Quaternion.identity);

            MechUnit2D mechScript = newMech.GetComponent<MechUnit2D>();
            if (mechScript != null) mechScript.InitUnitData(bindedProfile);

            HangarMenuUI.Instance.RefreshHangar();

            // 强行刷新全局顶部资源栏的电量刻度！
            PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
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