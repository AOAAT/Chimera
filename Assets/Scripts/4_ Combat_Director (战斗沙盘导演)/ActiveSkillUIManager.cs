using System.Collections.Generic;
using UnityEngine;

public class ActiveSkillUIManager : MonoBehaviour
{
    public static ActiveSkillUIManager Instance;

    [Header("=== UI 容器绑定 ===")]
    public Transform SlotContainer; // 技能格子的父节点
    public GameObject SkillSlotPrefab;

    // 👇【核心新增】：整个技能栏的父物体（包含背景、装饰等）
    public GameObject MainUIRoot;

    private Color[] palette = new Color[] {
        new Color(0.2f, 0.8f, 1f),
        new Color(1f, 0.6f, 0.2f),
        new Color(0.8f, 0.2f, 0.8f),
        new Color(0.4f, 1f, 0.4f)
    };

    private void Awake()
    {
        Instance = this;
        // 初始状态强制隐藏
        if (MainUIRoot != null) MainUIRoot.SetActive(false);
    }

    /// <summary>
    /// 控制技能栏的整体显隐
    /// </summary>
    public void SetVisibility(bool isVisible)
    {
        if (MainUIRoot != null)
        {
            MainUIRoot.SetActive(isVisible);
            Debug.Log($"<color=#FFD700>【UI状态切换】</color> 主动技能栏 -> {(isVisible ? "显示" : "隐藏")}");
        }
    }

    /// <summary>
    /// 彻底清理所有技能格子
    /// </summary>
    public void ClearUI()
    {
        foreach (Transform child in SlotContainer)
        {
            Destroy(child.gameObject);
        }
        SetVisibility(false);
    }

    public void BuildSkillUI(List<DamageReceiver> playerUnits)
    {
        ClearUI(); // 先清空旧的

        int validSkillCount = 0;
        KeyCode[] hotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        for (int i = 0; i < playerUnits.Count; i++)
        {
            DamageReceiver unit = playerUnits[i];
            MechSkillController skillCtrl = unit.GetComponent<MechSkillController>();
            if (skillCtrl == null) continue;

            Color assignedColor = palette[i % palette.Length];

            // 只有配置了主动技能的机甲才生成格子
            if (skillCtrl.SkillConfig != null && skillCtrl.SkillConfig.HasActiveSkill)
            {
                GameObject slotObj = Instantiate(SkillSlotPrefab, SlotContainer);
                ActiveSkillSlotUI slotUI = slotObj.GetComponent<ActiveSkillSlotUI>();

                KeyCode key = hotkeys[validSkillCount % hotkeys.Length];
                string keyName = (validSkillCount + 1).ToString();

                slotUI.Initialize(skillCtrl, key, keyName, assignedColor, unit.gameObject.name.Replace("[UNIT] ", ""));
                validSkillCount++;
            }
        }

        // 👇【关键】：只有生成了技能，且此时处于战斗状态，才显示出来
        if (validSkillCount > 0)
        {
            SetVisibility(true);
        }
    }
}