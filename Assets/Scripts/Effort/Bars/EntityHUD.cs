// --- 替换 EntityHUD.cs 全量代码 ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EntityHUD : MonoBehaviour
{
    [Header("=== UI 绑定 ===")]
    public Slider HPBar;
    public Slider APBar;
    public Transform BuffGrid;
    public GameObject BuffIconPrefab;
    public TMP_Text MechNameText;

    [Header("=== 动态格栅 ===")]
    public HealthBarGrid HPGrid;
    public HealthBarGrid APGrid;

    private DamageReceiver targetReceiver;
    private BuffManager targetBuffMgr;
    private Transform canvasTrans;

    public void Initialize(DamageReceiver receiver, BuffManager buffMgr)
    {
        // 先移除旧监听防止重复
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;

        targetReceiver = receiver;
        targetBuffMgr = buffMgr;

        if (HPBar != null) canvasTrans = HPBar.transform.parent;

        if (HPBar != null)
        {
            Image hpFill = HPBar.fillRect.GetComponent<Image>();
            if (hpFill != null) hpFill.color = receiver.isEnemy ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.9f, 0.2f);
        }

        if (MechNameText != null)
        {
            if (receiver.isEnemy) MechNameText.gameObject.SetActive(false);
            else
            {
                MechNameText.gameObject.SetActive(true);
                MechNameText.text = receiver.gameObject.name.Replace("[UNIT] ", "");
                MechNameText.color = Color.white;
            }
        }

        // 初始刷新
        UpdateBars();
        UpdateBuffs();

        // 【核心优化】：完全放弃 Update，只靠事件触发
        targetReceiver.OnStatsChanged += UpdateBars;
        targetBuffMgr.OnBuffsChanged += UpdateBuffs;
    }

    private void OnDestroy()
    {
        if (targetReceiver != null) targetReceiver.OnStatsChanged -= UpdateBars;
        if (targetBuffMgr != null) targetBuffMgr.OnBuffsChanged -= UpdateBuffs;
    }

    private void LateUpdate()
    {
        // 仅保留看板旋转，这部分计算量极小
        if (canvasTrans != null) canvasTrans.rotation = Quaternion.identity;
    }

    // 只有数值变了才会被调用
    private void UpdateBars()
    {
        if (targetReceiver == null) return;

        if (HPBar != null)
        {
            HPBar.maxValue = targetReceiver.MaxHP;
            HPBar.value = targetReceiver.CurrentHP;
            if (HPGrid != null) HPGrid.UpdateGrid(targetReceiver.MaxHP);
        }

        if (APBar != null)
        {
            APBar.maxValue = targetReceiver.MaxAP;
            APBar.value = targetReceiver.CurrentAP;
            APBar.gameObject.SetActive(targetReceiver.CurrentAP > 0);
            if (APGrid != null) APGrid.UpdateGrid(targetReceiver.MaxAP);
        }
    }

    private void UpdateBuffs()
    {
        if (targetBuffMgr == null || BuffGrid == null || BuffIconPrefab == null) return;

        // 清理旧图标
        for (int i = BuffGrid.childCount - 1; i >= 0; i--)
            Destroy(BuffGrid.GetChild(i).gameObject);

        // 这里如果未来 Buff 很多，也可以池化，目前先维持现状
        foreach (var buff in targetBuffMgr.GetActiveBuffs())
        {
            GameObject iconObj = Instantiate(BuffIconPrefab, BuffGrid);
            Image img = iconObj.GetComponent<Image>();
            TMP_Text stackTxt = iconObj.GetComponentInChildren<TMP_Text>();
            img.sprite = buff.Blueprint.BuffIcon;
            stackTxt.text = (buff.CurrentStacks > 1) ? buff.CurrentStacks.ToString() : "";
        }
    }
}