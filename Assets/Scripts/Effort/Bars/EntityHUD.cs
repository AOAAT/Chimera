// --- START OF FILE EntityHUD.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EntityHUD : MonoBehaviour
{
    [Header("=== UI 绑定 ===")]
    public Slider HPBar;
    public Slider APBar;
    public Transform BuffGrid;
    public GameObject BuffIconPrefab;

    [Header("=== 实体名称 (机甲专属) ===")]
    public TMP_Text MechNameText; // 👇 【新增这行代码】

    [Header("=== 动态格栅 ===")]
    public HealthBarGrid HPGrid;
    public HealthBarGrid APGrid;

    private DamageReceiver targetReceiver;
    private BuffManager targetBuffMgr;
    private Transform canvasTrans;

    public void Initialize(DamageReceiver receiver, BuffManager buffMgr)
    {
        targetReceiver = receiver;
        targetBuffMgr = buffMgr;

        if (HPBar != null) canvasTrans = HPBar.transform.parent;

        // 颜色区分：敌人红血，玩家绿血
        if (HPBar != null)
        {
            Image hpFill = HPBar.fillRect.GetComponent<Image>();
            if (hpFill != null) hpFill.color = receiver.isEnemy ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.9f, 0.2f);
        }

        // 👇【核心修复】：一初始化就处理名字！
        if (MechNameText != null)
        {
            if (receiver.isEnemy)
            {
                // 如果是敌人，直接隐藏文字！
                MechNameText.text = "";
                MechNameText.gameObject.SetActive(false);
            }
            else
            {
                // 如果是玩家的机甲，初始化时先用默认的白色名字显示出来
                // (等战斗开始，ActiveSkillUIManager 会用它专属的彩色名字将其覆盖)
                MechNameText.gameObject.SetActive(true);
                // 剔除 "[UNIT] " 前缀
                MechNameText.text = receiver.gameObject.name.Replace("[UNIT] ", "");
                MechNameText.color = Color.white;
            }
        }

        UpdateBars();

        if (targetReceiver != null) targetReceiver.OnStatsChanged += UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged += UpdateBuffs;
    }
    private void OnDestroy()
    {
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;
    }

    private void LateUpdate()
    {
        // 保证血条永远正对屏幕，不随模型翻转
        if (canvasTrans != null) canvasTrans.rotation = Quaternion.identity;
    }

    private void UpdateBars()
    {
        if (targetReceiver == null || HPBar == null || APBar == null) return;

        HPBar.maxValue = targetReceiver.MaxHP;
        HPBar.value = targetReceiver.CurrentHP;

        APBar.maxValue = targetReceiver.MaxAP;
        APBar.value = targetReceiver.CurrentAP;

        APBar.gameObject.SetActive(targetReceiver.CurrentAP > 0);

        if (HPGrid != null) HPGrid.UpdateGrid(targetReceiver.MaxHP);
        if (APGrid != null) APGrid.UpdateGrid(targetReceiver.MaxAP);
    }

    private void UpdateBuffs()
    {
        if (targetBuffMgr == null || BuffGrid == null || BuffIconPrefab == null) return;

        foreach (Transform child in BuffGrid) Destroy(child.gameObject);

        foreach (var buff in targetBuffMgr.GetActiveBuffs())
        {
            GameObject iconObj = Instantiate(BuffIconPrefab, BuffGrid);
            Image img = iconObj.GetComponent<Image>();
            TMP_Text stackTxt = iconObj.GetComponentInChildren<TMP_Text>();

            img.sprite = buff.Blueprint.BuffIcon;

            if (buff.CurrentStacks > 1) stackTxt.text = buff.CurrentStacks.ToString();
            else stackTxt.text = "";
        }
    }
}