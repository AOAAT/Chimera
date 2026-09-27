using UnityEngine;

public class MechSkillController : MonoBehaviour
{
    public ActiveSkillConfig SkillConfig { get; private set; }
    public float CurrentCooldown { get; private set; }
    public bool IsDead => receiver != null && receiver.CurrentHP <= 0;

    private DamageReceiver receiver;
    private RuntimeChimeraData runtimeData;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        receiver = GetComponent<DamageReceiver>();
        SkillConfig = data.CoreActiveSkill;

        if (SkillConfig != null)
        {
            Debug.Log($"[SkillCtrl] {data.UnitName} 初始化技能: {SkillConfig.SkillName}");
        }
    }

    private void Update()
    {
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;
        if (CurrentCooldown > 0) CurrentCooldown -= Time.deltaTime;
    }

    public bool TryCastSkill()
    {
        if (SkillConfig == null || IsDead || CurrentCooldown > 0) return false;

        if (GlobalCPManager.Instance != null && !GlobalCPManager.Instance.ModifyCP(-SkillConfig.CPCost))
        {
            return false;
        }

        CurrentCooldown = SkillConfig.Cooldown;

        ECAContext context = new ECAContext
        {
            PrimaryTarget = this.transform,
            SourceEntity = this.transform,
            ImpactPoint = this.transform.position,
            ChassisData = runtimeData,
            IsEnemyFire = false
        };

        // --- 👇 核心录入逻辑 ---
        if (SkillConfig.SkillName != "缸中之脑")
        {
            if (SkillCastHistory.Instance != null)
            {
                SkillCastHistory.Instance.Record(this.SkillConfig);
            }
            else
            {
                Debug.LogError("<color=red>【严重错误】</color> 场景中缺少 SkillCastHistory 物体！缸中之脑将无法工作！");
            }
        }

        // 顺序执行积木
        foreach (var action in SkillConfig.OnSkillCastActions)
        {
            if (action != null)
            {
                action.Execute(context);
                if (context.ExecutionAborted) break;
            }
        }

        return true;
    }

    // 设置高亮的方法保持不变...
    private Color originalColor = Color.clear;
    public void SetHighlight(bool isHighlighted)
    {
        if (IsDead) return;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs)
        {
            if (sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
            if (originalColor == Color.clear) originalColor = sr.color;
            sr.color = isHighlighted ? new Color(2f, 2f, 2f, 1f) : originalColor;
        }
    }
}