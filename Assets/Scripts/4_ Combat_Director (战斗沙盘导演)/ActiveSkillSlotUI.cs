// --- START OF FILE ActiveSkillSlotUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ActiveSkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image SkillIcon;
    public Image CooldownFill;
    public TMP_Text CPCostText;
    public TMP_Text HotkeyText;
    public TMP_Text MechNameText;
    public GameObject DeathOverlay;

    private MechSkillController bindedController;
    private KeyCode myHotkey;

    public void Initialize(MechSkillController controller, KeyCode key, string keyName, Color mechColor, string mechName)
    {
        bindedController = controller;
        myHotkey = key;

        SkillIcon.sprite = controller.SkillConfig.SkillIcon;
        CPCostText.text = controller.SkillConfig.CPCost.ToString();
        HotkeyText.text = keyName;

        MechNameText.text = mechName;
        MechNameText.color = mechColor; // 名字颜色对应机甲颜色！

        DeathOverlay.SetActive(false);
    }

    private void Update()
    {
        if (bindedController == null) return;

        if (bindedController.IsDead)
        {
            DeathOverlay.SetActive(true);
            CooldownFill.fillAmount = 0;
            return;
        }

        // 处理冷却表现
        float cd = bindedController.CurrentCooldown;
        float maxCd = bindedController.SkillConfig.Cooldown;
        CooldownFill.fillAmount = cd > 0 ? cd / maxCd : 0f;

        // 处理 CP 不足的颜色警告 (红字)
        bool cpEnough = GlobalCPManager.Instance != null && GlobalCPManager.Instance.CurrentCP >= bindedController.SkillConfig.CPCost;
        CPCostText.color = cpEnough ? Color.white : Color.red;

        // 快捷键按下触发
        if (Input.GetKeyDown(myHotkey) && cd <= 0 && cpEnough)
        {
            bindedController.TryCastSkill();
        }
    }

    public void OnSkillClicked()
    {
        bindedController.TryCastSkill();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bindedController != null) bindedController.SetHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bindedController != null) bindedController.SetHighlight(false);
    }
}