// --- 替换 EntityHUD.cs 全量代码 ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EntityHUD : MonoBehaviour
{
    [Header("=== 基础状态 UI 绑定 ===")]
    public Slider HPBar;
    public Slider APBar;
    public Transform BuffGrid;
    public GameObject BuffIconPrefab;
    public TMP_Text MechNameText;

    [Header("=== 动态格栅 (Health Grid) ===")]
    public HealthBarGrid HPGrid;
    public HealthBarGrid APGrid;

    [Header("=== 战术意图预告 (The Intent) ===")]
    public GameObject IntentRoot;         // 意图显示的总根节点
    public Image IntentIconImage;         // 意图图标 (如剑、盾、技能图标)
    public Image IntentFillImage;         // 进度环/条 (Image Type 需设为 Filled)

    private DamageReceiver targetReceiver;
    private BuffManager targetBuffMgr;
    private Transform canvasTrans;

    // 运行时意图计时
    private float intentDuration;
    private float intentTimer;
    private bool isShowingIntent = false;

    public void Initialize(DamageReceiver receiver, BuffManager buffMgr)
    {
        // 1. 清理旧监听，防止重复初始化导致的内存泄漏
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;

        targetReceiver = receiver;
        targetBuffMgr = buffMgr;

        if (HPBar != null) canvasTrans = HPBar.transform.parent;

        // 2. 根据阵营初始化血条颜色
        if (HPBar != null)
        {
            Image hpFill = HPBar.fillRect.GetComponent<Image>();
            if (hpFill != null)
                hpFill.color = receiver.isEnemy ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.9f, 0.2f);
        }

        // 3. 名字展示逻辑
        if (MechNameText != null)
        {
            if (receiver.isEnemy) MechNameText.gameObject.SetActive(false);
            else
            {
                MechNameText.gameObject.SetActive(true);
                // 过滤掉后缀，保持 UI 简洁
                MechNameText.text = receiver.gameObject.name.Replace("[UNIT] ", "").Replace("(Clone)", "");
                MechNameText.color = Color.white;
            }
        }

        // 4. 隐藏意图层
        if (IntentRoot != null) IntentRoot.SetActive(false);

        // 5. 执行初始刷新
        UpdateBars();
        UpdateBuffs();

        // 6. 【性能优化】：完全放弃 Update 常驻刷新，改用事件驱动
        targetReceiver.OnStatsChanged += UpdateBars;
        targetBuffMgr.OnBuffsChanged += UpdateBuffs;
    }

    private void OnDestroy()
    {
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;
    }

    private void Update()
    {
        // 意图进度条刷新：这是 HUD 中唯一需要 Update 的动态部分
        if (isShowingIntent)
        {
            intentTimer -= Time.deltaTime;
            if (IntentFillImage != null && intentDuration > 0)
            {
                // 进度条从 1 归零，代表蓄力完成
                IntentFillImage.fillAmount = Mathf.Clamp01(intentTimer / intentDuration);
            }

            if (intentTimer <= 0)
            {
                // 计时结束但不在这里 Hide，由 EnemyBrain 调用 HideIntent
            }
        }
    }

    private void LateUpdate()
    {
        // 【看板效果】：确保 UI 永远正对着摄像机，不随父物体旋转
        if (canvasTrans != null) canvasTrans.rotation = Quaternion.identity;
    }

    // ==========================================
    // ⚔️ 战术意图接口 (由 EnemyBrain 呼叫)
    // ==========================================

    /// <summary>
    /// 开启意图预告
    /// </summary>
    /// <param name="icon">技能图标</param>
    /// <param name="duration">蓄力/持续时间</param>
    public void ShowIntent(Sprite icon, float duration)
    {
        if (IntentRoot == null) return;

        IntentRoot.SetActive(true);
        if (IntentIconImage != null) IntentIconImage.sprite = icon;

        intentDuration = duration;
        intentTimer = duration;
        isShowingIntent = true;

        if (IntentFillImage != null) IntentFillImage.fillAmount = 1f;
    }

    /// <summary>
    /// 关闭意图预告
    /// </summary>
    public void HideIntent()
    {
        if (IntentRoot == null) return;

        isShowingIntent = false;
        IntentRoot.SetActive(false);
    }

    // ==========================================
    // 📊 数据刷新逻辑 (事件驱动)
    // ==========================================

    private void UpdateBars()
    {
        if (targetReceiver == null) return;

        // 刷新 HP
        if (HPBar != null)
        {
            HPBar.maxValue = targetReceiver.MaxHP;
            HPBar.value = targetReceiver.CurrentHP;
            if (HPGrid != null) HPGrid.UpdateGrid(targetReceiver.MaxHP);
        }

        // 刷新 AP (护甲血条)
        if (APBar != null)
        {
            APBar.maxValue = targetReceiver.MaxAP;
            APBar.value = targetReceiver.CurrentAP;

            // 如果护甲为 0，直接隐藏护甲条节省视觉空间
            APBar.gameObject.SetActive(targetReceiver.CurrentAP > 0);

            if (APGrid != null) APGrid.UpdateGrid(targetReceiver.MaxAP);
        }
    }

    private void UpdateBuffs()
    {
        if (targetBuffMgr == null || BuffGrid == null || BuffIconPrefab == null) return;

        // 1. 暴力清理旧的 Buff 图标 (未来单位多且 Buff 频繁时建议使用对象池优化)
        for (int i = BuffGrid.childCount - 1; i >= 0; i--)
            Destroy(BuffGrid.GetChild(i).gameObject);

        // 2. 重新渲染当前所有活跃 Buff
        foreach (var buff in targetBuffMgr.GetActiveBuffs())
        {
            if (buff.Blueprint == null || buff.Blueprint.BuffIcon == null) continue;

            GameObject iconObj = Instantiate(BuffIconPrefab, BuffGrid);
            Image img = iconObj.GetComponent<Image>();
            TMP_Text stackTxt = iconObj.GetComponentInChildren<TMP_Text>();

            img.sprite = buff.Blueprint.BuffIcon;

            // 层数大于 1 时才显示数字
            if (stackTxt != null)
                stackTxt.text = (buff.CurrentStacks > 1) ? buff.CurrentStacks.ToString() : "";
        }
    }
}