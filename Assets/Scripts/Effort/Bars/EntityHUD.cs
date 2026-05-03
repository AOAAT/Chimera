using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EntityHUD : MonoBehaviour
{
    [Header("=== 结构化显隐控制 (需挂载 CanvasGroup) ===")]
    public CanvasGroup SurvivalGroup;   // 血条、护甲、名字
    public CanvasGroup IntentGroup;     // 意图图标根节点
    public CanvasGroup BuffGroup;       // Buff 列表根节点

    [Header("=== 基础 UI 绑定 ===")]
    public Slider HPBar;
    public Slider APBar;
    public Transform BuffGrid;
    public GameObject BuffIconPrefab;
    public TMP_Text MechNameText;

    [Header("=== 动态格栅 ===")]
    public HealthBarGrid HPGrid;
    public HealthBarGrid APGrid;

    [Header("=== 意图显示表现 ===")]
    public Image IntentIconImage;
    public Image IntentFillImage;

    [Header("=== 渐变参数 ===")]
    public float ShowDuration = 3.0f;     // 生存信息显示时长
    public float FadeInSpeed = 5.0f;      // 渐入速度 (数值越大越快)
    public float FadeOutSpeed = 2.0f;     // 渐出速度
    public bool AlwaysShowIfPlayer = true;

    private DamageReceiver targetReceiver;
    private BuffManager targetBuffMgr;
    private Transform canvasTrans;

    // 内部状态追踪
    private float survivalDisplayTimer;
    private bool isIntentActive = false;
    private float intentDuration;
    private float intentTimer;

    public void Initialize(DamageReceiver receiver, BuffManager buffMgr)
    {
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= OnStatsChanged;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;

        targetReceiver = receiver;
        targetBuffMgr = buffMgr;

        if (HPBar != null) canvasTrans = HPBar.transform.parent;

        // 初始化视觉状态
        if (HPBar != null)
        {
            Image hpFill = HPBar.fillRect.GetComponent<Image>();
            if (hpFill != null)
                hpFill.color = receiver.isEnemy ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.9f, 0.2f);
        }

        if (MechNameText != null)
        {
            MechNameText.gameObject.SetActive(!receiver.isEnemy);
            if (!receiver.isEnemy) MechNameText.text = receiver.gameObject.name.Replace("[UNIT] ", "").Replace("(Clone)", "");
        }

        // --- 核心初始化：强制静默 ---
        if (SurvivalGroup != null) SurvivalGroup.alpha = 0;
        if (IntentGroup != null) IntentGroup.alpha = 0;
        if (BuffGroup != null) BuffGroup.alpha = 0;

        UpdateBars();
        UpdateBuffs();

        // 初始逻辑判定
        survivalDisplayTimer = (AlwaysShowIfPlayer && !receiver.isEnemy) ? ShowDuration : 0;

        targetReceiver.OnStatsChanged += OnStatsChanged;
        targetBuffMgr.OnBuffsChanged += UpdateBuffs;
    }

    private void OnStatsChanged()
    {
        survivalDisplayTimer = ShowDuration;
        UpdateBars();
    }

    private void Update()
    {
        HandleAllFading();
        HandleIntentLogic();
    }

    // ==========================================
    // ✨ 程序化渐入渐出引擎
    // ==========================================
    private void HandleAllFading()
    {
        if (targetReceiver == null) return;

        // 1. 生存组 Fading
        float targetSurvivalAlpha = 0;
        bool forceShow = (!targetReceiver.isEnemy && AlwaysShowIfPlayer) ||
                         (BattleCommandManager.Instance != null && BattleCommandManager.Instance.SelectedUnit != null && BattleCommandManager.Instance.SelectedUnit.gameObject == targetReceiver.gameObject);

        if (forceShow || survivalDisplayTimer > 0) targetSurvivalAlpha = 1;
        if (survivalDisplayTimer > 0 && !forceShow) survivalDisplayTimer -= Time.deltaTime;

        UpdateGroupAlpha(SurvivalGroup, targetSurvivalAlpha);

        // 2. 意图组 Fading (有则显，无则隐)
        float targetIntentAlpha = isIntentActive ? 1 : 0;
        UpdateGroupAlpha(IntentGroup, targetIntentAlpha);

        // 3. Buff 组 Fading (有则显，无则隐)
        float targetBuffAlpha = (targetBuffMgr != null && targetBuffMgr.GetActiveBuffs().Count > 0) ? 1 : 0;
        UpdateGroupAlpha(BuffGroup, targetBuffAlpha);
    }

    private void UpdateGroupAlpha(CanvasGroup group, float targetAlpha)
    {
        if (group == null) return;

        // 根据目标透明度选择速度（渐入快，渐出慢，手感更好）
        float speed = (targetAlpha > group.alpha) ? FadeInSpeed : FadeOutSpeed;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * speed);

        // 性能优化：Alpha 为 0 时关闭射线检测
        group.blocksRaycasts = group.alpha > 0.1f;
    }

    private void HandleIntentLogic()
    {
        if (isIntentActive)
        {
            intentTimer -= Time.deltaTime;
            if (IntentFillImage != null && intentDuration > 0)
                IntentFillImage.fillAmount = Mathf.Clamp01(intentTimer / intentDuration);
        }
    }

    private void LateUpdate()
    {
        if (canvasTrans != null) canvasTrans.rotation = Quaternion.identity;
    }

    // ==========================================
    // ⚔️ 外部接口
    // ==========================================

    public void ShowIntent(Sprite icon, float duration)
    {
        if (IntentGroup == null) return;

        // 更换图标
        if (IntentIconImage != null) IntentIconImage.sprite = icon;

        intentDuration = duration;
        intentTimer = duration;
        isIntentActive = true; // 触发渐入
        if (IntentFillImage != null) IntentFillImage.fillAmount = 1f;
    }

    public void HideIntent()
    {
        isIntentActive = false; // 触发渐出
    }

    private void UpdateBars()
    {
        if (targetReceiver == null) return;
        if (HPBar != null) { HPBar.maxValue = targetReceiver.MaxHP; HPBar.value = targetReceiver.CurrentHP; if (HPGrid != null) HPGrid.UpdateGrid(targetReceiver.MaxHP); }
        if (APBar != null) { APBar.maxValue = targetReceiver.MaxAP; APBar.value = targetReceiver.CurrentAP; APBar.gameObject.SetActive(targetReceiver.CurrentAP > 0); if (APGrid != null) APGrid.UpdateGrid(targetReceiver.MaxAP); }
    }

    private void UpdateBuffs()
    {
        if (targetBuffMgr == null || BuffGrid == null || BuffIconPrefab == null) return;
        for (int i = BuffGrid.childCount - 1; i >= 0; i--) Destroy(BuffGrid.GetChild(i).gameObject);
        foreach (var buff in targetBuffMgr.GetActiveBuffs())
        {
            if (buff.Blueprint == null || buff.Blueprint.BuffIcon == null) continue;
            GameObject iconObj = Instantiate(BuffIconPrefab, BuffGrid);
            BuffIconUI buffScript = iconObj.GetComponent<BuffIconUI>();
            if (buffScript != null) buffScript.Initialize(buff);
        }
    }

    private void OnDestroy()
    {
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= OnStatsChanged;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;
    }
}