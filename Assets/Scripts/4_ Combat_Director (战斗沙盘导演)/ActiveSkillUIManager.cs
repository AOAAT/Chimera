// --- START OF FILE ActiveSkillUIManager.cs ---
using System.Collections.Generic;
using UnityEngine;
using TMPro; // 如果没有这行请加上

public class ActiveSkillUIManager : MonoBehaviour
{
    public static ActiveSkillUIManager Instance; // 👇 加了单例

    public Transform SlotContainer;
    public GameObject SkillSlotPrefab;

    private Color[] palette = new Color[] {
        new Color(0.2f, 0.8f, 1f),   // 青蓝
        new Color(1f, 0.6f, 0.2f),   // 橙黄
        new Color(0.8f, 0.2f, 0.8f), // 紫红
        new Color(0.4f, 1f, 0.4f)    // 亮绿
    };

    private void Awake() { Instance = this; }

    // 👇 参数改成 List<DamageReceiver> 适配战斗导演
    public void BuildSkillUI(List<DamageReceiver> playerUnits)
    {
        foreach (Transform child in SlotContainer) Destroy(child.gameObject);

        int validSkillCount = 0;
        KeyCode[] hotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        for (int i = 0; i < playerUnits.Count; i++)
        {
            DamageReceiver unit = playerUnits[i];
            MechSkillController skillCtrl = unit.GetComponent<MechSkillController>();

            Color assignedColor = palette[i % palette.Length];
            EntityHUD hud = unit.GetComponentInChildren<EntityHUD>();

            // 头顶名字赋色
            if (hud != null && hud.MechNameText != null)
            {
                hud.MechNameText.text = unit.gameObject.name.Replace("[UNIT] ", "");
                hud.MechNameText.color = assignedColor;
            }

            if (skillCtrl != null && skillCtrl.SkillConfig != null && skillCtrl.SkillConfig.HasActiveSkill)
            {
                GameObject slotObj = Instantiate(SkillSlotPrefab, SlotContainer);
                ActiveSkillSlotUI slotUI = slotObj.GetComponent<ActiveSkillSlotUI>();

                KeyCode key = hotkeys[validSkillCount % hotkeys.Length];
                string keyName = (validSkillCount + 1).ToString();

                slotUI.Initialize(skillCtrl, key, keyName, assignedColor, unit.gameObject.name.Replace("[UNIT] ", ""));
                validSkillCount++;
            }
        }
    }
}