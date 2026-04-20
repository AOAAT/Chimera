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
        // 1. 基础前置判定：配置是否存在、是否阵亡、冷却是否就绪
        if (SkillConfig == null || IsDead || CurrentCooldown > 0) return false;

        // 2. 资源消耗判定
        if (GlobalCPManager.Instance != null && !GlobalCPManager.Instance.ModifyCP(-SkillConfig.CPCost))
        {
            // Debug.LogWarning($"【指令拒绝】CP不足，无法释放技能：{SkillConfig.SkillName}");
            return false;
        }

        // 3. 记录冷却起算点
        CurrentCooldown = SkillConfig.Cooldown;

        // 4. 构建 ECA 执行上下文
        ECAContext context = new ECAContext
        {
            PrimaryTarget = this.transform,
            SourceEntity = this.transform,
            ImpactPoint = this.transform.position,
            ChassisData = runtimeData,
            IsEnemyFire = false
        };

        // 5. 👇【核心新增】：技能历史录入
        // 只有当当前释放的技能“不是”缸中之脑本身时，才进行录入。
        // 注意：请确保你在 ScriptableObject 图纸里填写的技能名称准确为 "缸中之脑"
        if (SkillCastHistory.Instance != null && SkillConfig.SkillName != "缸中之脑")
        {
            SkillCastHistory.Instance.Record(this.SkillConfig);
        }

        // 6. 顺序执行 ECA 积木链
        foreach (var action in SkillConfig.OnSkillCastActions)
        {
            if (action != null)
            {
                action.Execute(context);

                // 如果积木内部触发了熔断（比如镜像复刻失败），则停止执行后续积木
                if (context.ExecutionAborted) break;
            }
        }

        // 7. 执行成功回执
        // Debug.Log($"<color=#00FF00>【战术执行】</color> [{runtimeData.UnitName}] 成功释放了：{SkillConfig.SkillName}");
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
            // 缓存原始颜色逻辑
            if (originalColor == Color.clear) originalColor = sr.color;
            sr.color = isHighlighted ? new Color(2f, 2f, 2f, 1f) : originalColor;
        }
    }
}