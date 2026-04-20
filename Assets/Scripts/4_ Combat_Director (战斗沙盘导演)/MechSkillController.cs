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

        foreach (var action in SkillConfig.OnSkillCastActions)
        {
            if (action != null) action.Execute(context);
            if (context.ExecutionAborted) break;
        }

        return true; // 确保 return 之前没有多余逻辑
    }

    private Color originalColor = Color.clear;
    public void SetHighlight(bool isHighlighted)
    {
        if (IsDead) return;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs)
        {
            if (sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
            // 缓存原始颜色逻辑
            if (originalColor == Color.clear) originalColor = sr.color;
            sr.color = isHighlighted ? new Color(2f, 2f, 2f, 1f) : originalColor;
        }
    }
}