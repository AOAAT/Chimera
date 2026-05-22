using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarSlotUI : MonoBehaviour
{
    [Header("=== RTS 生产配置 ===")]
    public GameObject MechPrefab; // 指向 [Base_Mech_Unit]
    public GameObject EmptyStateObj;
    public GameObject OccupiedStateObj;

    [Header("=== 视觉展现 ===")]
    public RectTransform UnitVisualContainer;
    public TMP_Text UnitNameText;
    public float PreviewScale = 1.0f;

    private SavedUnitProfile bindedProfile;
    private int mySlotIndex = -1;

    public void RefreshSlot(int index, SavedUnitProfile profile)
    {
        mySlotIndex = index;
        bindedProfile = profile;

        bool hasUnit = (profile != null && profile.ChassisData != null);
        EmptyStateObj.SetActive(!hasUnit);
        OccupiedStateObj.SetActive(hasUnit);

        if (hasUnit)
        {
            UnitNameText.text = profile.UnitName;
            BuildUnitVisual(profile);
        }
    }

    public void OnSlotClicked()
    {
        if (bindedProfile == null)
        {
            // 槽位为空：打开车间新建机甲
            HangarMenuUI.Instance.TriggerCreateNewUnit(mySlotIndex);
        }
        else
        {
            // 槽位已有：Shift+点击=生产，普通点击=详情
            if (Input.GetKey(KeyCode.LeftShift))
            {
                TryInstantSpawn();
            }
            else
            {
                HangarMenuUI.Instance.TriggerOpenUnitDetail(mySlotIndex, bindedProfile);
            }
        }
    }

    private void TryInstantSpawn()
    {
       

        // 2. 确定生产位置 (相机中心点吸附网格)
        Vector3 spawnPos = Camera.main.transform.position;
        spawnPos.z = 0;
        if (RTSGridSystem.Instance != null)
        {
            spawnPos = RTSGridSystem.Instance.GetSnappedWorldPos(spawnPos);
        }

        // 3. 实例化与初始化
        GameObject newMech = Instantiate(MechPrefab, spawnPos, Quaternion.identity);
        MechUnit2D mechScript = newMech.GetComponent<MechUnit2D>();

        // 注入数据 (关键：此方法内部会运行 Assemble 逻辑)
        mechScript.InitUnitData(bindedProfile);

        // 4. 🔥 物理标准契约重塑 (RTS 核心：圆形碰撞)
        var oldBox = newMech.GetComponent<BoxCollider2D>();
        if (oldBox != null) oldBox.enabled = false;

        CircleCollider2D circle = newMech.GetComponent<CircleCollider2D>();
        if (circle == null) circle = newMech.AddComponent<CircleCollider2D>();
        circle.radius = 0.35f;

        // 5. 表现反馈
        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_PowerOn);
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();

        Debug.Log($"<color=#00FF00>【战地调度】机甲 {bindedProfile.UnitName} 已空降成功。</color>");
    }

    // 渲染 UI 缩略图逻辑 (保持原样，不省略)
    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        foreach (Transform child in UnitVisualContainer) Destroy(child.gameObject);
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
            GameObject slotObj = new GameObject($"Socket_{slotIdx}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * 100f;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject visObj = new GameObject("Visual");
            visObj.transform.SetParent(slotRect, false);
            visObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            visObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();
            compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * 100f;
        }
    }
}