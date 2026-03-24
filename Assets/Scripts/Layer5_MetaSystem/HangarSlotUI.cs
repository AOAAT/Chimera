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
    public GameObject MechPrefab; // 【新增】用来生成 3D/2D 肉体的图纸

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

        // 👇【主程防坑】：不但要防 profile 本身为 null，还要防 Unity 自动生成的没有 ChassisData 的空壳！
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

            // 👇【核心修复】：给格子也加上当场计算 MaxHP 的逻辑！
            float maxHP = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedHP);
            foreach (string compID in profile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null && comp.BaseData != null)
                    maxHP += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.AddedHP);
            }

            UnitNameText.text = profile.UnitName;
            // 👇 统一为当前血量 / 最大血量
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
        // 👇【双保险拦截】：如果没有底盘图纸，直接返回 0，绝不往下走！
        if (profile == null || profile.ChassisData == null) return 0f;

        float power = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);

        // 👇【防呆拦截 2】：如果在测试时 PlayerInventoryManager 还没准备好，也安全退出
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

        // 👇【核心修复】：将判断标准对齐 RefreshSlot！
        // 只有当档案不为空，且档案里确实装载了底盘 (ChassisData) 时，才算真正“有车”！
        bool isEmptySlot = (bindedProfile == null || bindedProfile.ChassisData == null);

        if (isEmptySlot)
        {
            Debug.Log($"【机库流转】{mySlotIndex} 号车位为空，已引导长官前往【组装车间】！");
            // 是空车，正确跳转至新建组装页
            HangarMenuUI.Instance.TriggerCreateNewUnit(mySlotIndex);
        }
        else
        {
            Debug.Log($"【机库流转】{mySlotIndex} 号车位已停放机甲，正在打开【详情档案】！");
            // 有货，跳转至详情页
            HangarMenuUI.Instance.TriggerOpenUnitDetail(mySlotIndex, bindedProfile);
        }
    }

    // ==========================================
    // 拖拽系统核心
    // ==========================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (bindedProfile == null || bindedProfile.IsDeployed || eventData.button != PointerEventData.InputButton.Left) return;

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
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;

            // 情况 A：UI 雷达扫描（仓库内部换位）
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
                        Debug.LogWarning("【防撞预警】目标车位上的机甲正在前线交战，底盘锁死，无法挪车！");
                        return;
                    }

                    SavedUnitProfile temp = inventory[sourceIndex];
                    inventory[sourceIndex] = inventory[targetIndex];
                    inventory[targetIndex] = temp;

                    Debug.Log($"【换位成功】长官，{sourceIndex} 号车位与 {targetIndex} 号车位的资产已互换！");
                    HangarMenuUI.Instance.RefreshHangar();
                    return;
                }
            }

            // 👇👇👇 情况 B：【纯 2D 坐标系天降正义！】 👇👇👇
            if (MechPrefab == null)
            {
                Debug.LogError("【防呆警告】长官！你还没有在 HangarSlotUI 预制体里挂载 MechPrefab！");
                return;
            }

            // 1. 屏幕坐标转 2D 世界坐标
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(eventData.position);
            Vector2 dropPos2D = new Vector2(worldPoint.x, worldPoint.y);

            // 2. 发射 2D 探针，看看有没有扎到碰撞体
            Collider2D hitCollider = Physics2D.OverlapPoint(dropPos2D);

            // 3. 校验是不是部署地块
            if (hitCollider != null && hitCollider.CompareTag("DeployZone"))
            {
                bindedProfile.IsDeployed = true;

                // 生成机甲！Z 轴强制锁死在 0，防止在 2D 游戏里看不见
                Vector3 spawnPos = new Vector3(dropPos2D.x, dropPos2D.y, 0f);
                GameObject newMech = Instantiate(MechPrefab, spawnPos, Quaternion.identity);

                // 灵魂注射！
                MechUnit2D mechScript = newMech.GetComponent<MechUnit2D>();
                if (mechScript != null)
                {
                    mechScript.InitUnitData(bindedProfile);
                }

                Debug.Log($"【天降正义】[{bindedProfile.UnitName}] 已成功部署到战场坐标: {spawnPos}");

                // 刷新机库，自己置灰
                HangarMenuUI.Instance.RefreshHangar();
            }
            else
            {
                Debug.LogWarning("【空投失败】长官，该坐标没有铺设 DeployZone 地板，或者扔到了虚空里！");
            }
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