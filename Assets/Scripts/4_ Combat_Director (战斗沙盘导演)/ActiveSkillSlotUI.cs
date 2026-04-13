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
    private KeyCode myNumpadKey; // 👇 兼容小键盘

    public void Initialize(MechSkillController controller, KeyCode key, string keyName, Color mechColor, string mechName)
    {
        bindedController = controller;
        myHotkey = key;

        // 智能映射：如果分配的是 Alpha1(键盘左上角)，自动绑定对应的 Keypad1(右侧小键盘)
        myNumpadKey = myHotkey + (KeyCode.Keypad1 - KeyCode.Alpha1);

        SkillIcon.sprite = controller.SkillConfig.SkillIcon;
        CPCostText.text = controller.SkillConfig.CPCost.ToString();
        HotkeyText.text = keyName;

        MechNameText.text = mechName;
        MechNameText.color = mechColor;

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

        // 处理冷却圈旋转表现 (1 -> 0)
        float cd = bindedController.CurrentCooldown;
        float maxCd = bindedController.SkillConfig.Cooldown;
        CooldownFill.fillAmount = cd > 0 ? cd / maxCd : 0f;

        bool cpEnough = GlobalCPManager.Instance != null && GlobalCPManager.Instance.CurrentCP >= bindedController.SkillConfig.CPCost;
        CPCostText.color = cpEnough ? Color.white : Color.red;

        // 👇【核心修复】：同时监听主键盘和小键盘！
        if (Input.GetKeyDown(myHotkey) || Input.GetKeyDown(myNumpadKey))
        {
            ExecuteSkillRequest(cpEnough, cd);
        }
    }

    public void OnSkillClicked()
    {
        // 鼠标点击走同样的判定
        bool cpEnough = GlobalCPManager.Instance != null && GlobalCPManager.Instance.CurrentCP >= bindedController.SkillConfig.CPCost;
        float cd = bindedController.CurrentCooldown;

        ExecuteSkillRequest(cpEnough, cd);
    }

    private void ExecuteSkillRequest(bool cpEnough, float cd)
    {
        if (cd > 0)
        {
            Debug.Log($"【技能拒绝】[{bindedController.SkillConfig.SkillName}] 仍在冷却中！还剩 {cd:F1} 秒");
            return;
        }

        if (!cpEnough)
        {
            Debug.Log($"【技能拒绝】[{bindedController.SkillConfig.SkillName}] CP能量不足！");
            // 这里可以未来接一个按钮闪红的抖动动画
            return;
        }

        // 一切就绪，开火！
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