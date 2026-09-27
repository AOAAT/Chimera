using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ActiveSkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("=== 基础图标与进度 ===")]
    public Image SkillIcon;
    public Image CooldownFill;
    public TMP_Text CPCostText;
    public TMP_Text HotkeyText;
    public TMP_Text MechNameText;

    // 👇【核心新增】：显示技能名称的文本框
    [Header("=== 技能名称显示 ===")]
    public TMP_Text SkillNameDisplay;

    public GameObject DeathOverlay;

    private MechSkillController bindedController;
    private KeyCode myHotkey;
    private KeyCode myNumpadKey;

    public void Initialize(MechSkillController controller, KeyCode key, string keyName, Color mechColor, string mechName)
    {
        bindedController = controller;
        myHotkey = key;
        myNumpadKey = myHotkey + (KeyCode.Keypad1 - KeyCode.Alpha1);

        SkillIcon.sprite = controller.SkillConfig.SkillIcon;
        CPCostText.text = controller.SkillConfig.CPCost.ToString();
        HotkeyText.text = keyName;
        MechNameText.text = mechName;
        MechNameText.color = mechColor;

        // 👇 初始化显示技能名字
        if (SkillNameDisplay != null)
        {
            SkillNameDisplay.text = controller.SkillConfig.SkillName;
        }

        DeathOverlay.SetActive(false);

        // 如果是缸中之脑，订阅变身事件
        if (controller.SkillConfig.SkillName == "缸中之脑")
        {
            if (SkillCastHistory.Instance != null)
            {
                SkillCastHistory.Instance.OnMemoryChanged += SyncMirrorVisuals;
                SyncMirrorVisuals(SkillCastHistory.Instance.MemorizedSkill);
            }
        }
    }

    private void OnDestroy()
    {
        if (SkillCastHistory.Instance != null)
        {
            SkillCastHistory.Instance.OnMemoryChanged -= SyncMirrorVisuals;
        }
    }

    private void SyncMirrorVisuals(ActiveSkillConfig newSkill)
    {
        if (newSkill == null)
        {
            SkillIcon.color = new Color(1, 1, 1, 0.4f);
            if (SkillNameDisplay != null) SkillNameDisplay.text = "等待解析...";
            return;
        }

        // 👇【变身】：瞬间替换图标和文本！
        SkillIcon.sprite = newSkill.SkillIcon;
        SkillIcon.color = Color.white;

        if (SkillNameDisplay != null)
        {
            // 给复刻出来的名字加个特殊的颜色区分
            SkillNameDisplay.text = $"<color=#00FFFF>[复刻]</color> {newSkill.SkillName}";
        }
    }

    private void Update()
    {

        if (bindedController == null || bindedController.gameObject == null)
        {
            return;
        }
        // ------------------------------------------

        if (bindedController.IsDead)
        {
            DeathOverlay.SetActive(true);
            CooldownFill.fillAmount = 0;
            return;
        }


        float cd = bindedController.CurrentCooldown;
        float maxCd = bindedController.SkillConfig.Cooldown;
        CooldownFill.fillAmount = cd > 0 ? cd / maxCd : 0f;

        bool hasMemoryIfBrain = (bindedController.SkillConfig.SkillName != "缸中之脑") ||
                                (SkillCastHistory.Instance != null && SkillCastHistory.Instance.MemorizedSkill != null);

        bool cpEnough = GlobalCPManager.Instance != null && GlobalCPManager.Instance.CurrentCP >= bindedController.SkillConfig.CPCost;

        CPCostText.color = cpEnough ? Color.white : Color.red;

        if (!hasMemoryIfBrain) SkillIcon.color = new Color(1, 1, 1, 0.3f);
        else SkillIcon.color = Color.white;

        if (Input.GetKeyDown(myHotkey) || Input.GetKeyDown(myNumpadKey))
        {
            ExecuteSkillRequest(cpEnough && hasMemoryIfBrain, cd);
        }

        bool isMirrorBrain = bindedController.SkillConfig.SkillName.Contains("缸中");
        bool hasMemory = SkillCastHistory.Instance != null && SkillCastHistory.Instance.MemorizedSkill != null;

        // 如果是缸中之脑且没记忆，强制置灰图标且不可交互
        if (isMirrorBrain && !hasMemory)
        {
            SkillIcon.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            // 如果玩家强行点，这里也可以拦截
        }
    }

    public void OnSkillClicked()
    {
        bool hasMemoryIfBrain = (bindedController.SkillConfig.SkillName != "缸中之脑") ||
                                (SkillCastHistory.Instance != null && SkillCastHistory.Instance.MemorizedSkill != null);
        bool cpEnough = GlobalCPManager.Instance != null && GlobalCPManager.Instance.CurrentCP >= bindedController.SkillConfig.CPCost;
        float cd = bindedController.CurrentCooldown;

        ExecuteSkillRequest(cpEnough && hasMemoryIfBrain, cd);
    }

    private void ExecuteSkillRequest(bool canCast, float cd)
    {
        if (cd > 0 || !canCast) return;
        bindedController.TryCastSkill();
    }

    public void OnPointerEnter(PointerEventData eventData) { if (bindedController != null) bindedController.SetHighlight(true); }
    public void OnPointerExit(PointerEventData eventData) { if (bindedController != null) bindedController.SetHighlight(false); }
}