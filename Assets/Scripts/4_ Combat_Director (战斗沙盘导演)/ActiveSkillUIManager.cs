// --- START OF FILE ActiveSkillUIManager.cs ---
using System.Collections.Generic;
using UnityEngine;

public class ActiveSkillUIManager : MonoBehaviour
{
    public static ActiveSkillUIManager Instance;

    [Header("=== UI 绑定 ===")]
    public Transform SlotContainer;
    public GameObject SkillSlotPrefab;

    private Color[] palette = new Color[] {
        new Color(0.2f, 0.8f, 1f),
        new Color(1f, 0.6f, 0.2f),
        new Color(0.8f, 0.2f, 0.8f),
        new Color(0.4f, 1f, 0.4f)
    };

    private void Awake() { Instance = this; }

    public void BuildSkillUI(List<DamageReceiver> playerUnits)
    {
        Debug.Log($"<color=#FFD700>[SkillUI_Manager]</color> 收到指令，开始为 {playerUnits.Count} 台机甲排布技能栏...");

        if (SlotContainer == null || SkillSlotPrefab == null)
        {
            Debug.LogError("<color=#FF0000>[SkillUI_Manager] 严重错误！SlotContainer 或 SkillSlotPrefab 没有在面板上拖拽赋值！</color>");
            return;
        }

        foreach (Transform child in SlotContainer) Destroy(child.gameObject);

        int validSkillCount = 0;
        KeyCode[] hotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        for (int i = 0; i < playerUnits.Count; i++)
        {
            DamageReceiver unit = playerUnits[i];
            MechSkillController skillCtrl = unit.GetComponent<MechSkillController>();

            Color assignedColor = palette[i % palette.Length];
            EntityHUD hud = unit.GetComponentInChildren<EntityHUD>();

            if (hud != null && hud.MechNameText != null)
            {
                hud.MechNameText.text = unit.gameObject.name.Replace("[UNIT] ", "");
                hud.MechNameText.color = assignedColor;
            }

            if (skillCtrl == null)
            {
                Debug.LogWarning($"<color=#FF0000>[SkillUI_Manager]</color> 机甲 {unit.gameObject.name} 身上没找到 MechSkillController！");
                continue;
            }

            if (skillCtrl.SkillConfig != null && skillCtrl.SkillConfig.HasActiveSkill)
            {
                GameObject slotObj = Instantiate(SkillSlotPrefab, SlotContainer);
                ActiveSkillSlotUI slotUI = slotObj.GetComponent<ActiveSkillSlotUI>();

                KeyCode key = hotkeys[validSkillCount % hotkeys.Length];
                string keyName = (validSkillCount + 1).ToString();

                slotUI.Initialize(skillCtrl, key, keyName, assignedColor, unit.gameObject.name.Replace("[UNIT] ", ""));
                validSkillCount++;
                Debug.Log($"<color=#00FF00>[SkillUI_Manager]</color> 成功生成技能格子：{skillCtrl.SkillConfig.SkillName}");
            }
            else
            {
                Debug.Log($"<color=#888888>[SkillUI_Manager]</color> 掠过机甲 {unit.gameObject.name}，因为它没有主动技能。");
            }
        }
    }
}