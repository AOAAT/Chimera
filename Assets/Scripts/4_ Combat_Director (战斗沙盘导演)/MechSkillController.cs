// --- START OF FILE MechSkillController.cs ---
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

        // 直接从黑盒里取！绝对安全！
        SkillConfig = data.CoreActiveSkill;

        if (SkillConfig != null && SkillConfig.HasActiveSkill)
        {
            Debug.Log($"<color=#00FF00>[MechSkillController]</color> 机甲 {data.UnitName} 初始化主动技能成功：{SkillConfig.SkillName}");
            CurrentCooldown = 0f;
        }
        else
        {
            Debug.Log($"<color=#888888>[MechSkillController]</color> 机甲 {data.UnitName} 没有任何主动技能配置。");
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
            Debug.LogWarning($"【指令拒绝】CP 不足，需要 {SkillConfig.CPCost}！");
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

        foreach (var action in SkillConfig.OnSkillCastActions)
        {
            if (action != null) action.Execute(context);
            if (context.ExecutionAborted) break;
        }

        Debug.Log($"<color=#00FF00>【战术执行】</color> [{runtimeData.UnitName}] 释放了 [{SkillConfig.SkillName}]！");
        return true;
    }

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