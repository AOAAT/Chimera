// --- START OF FILE MechSkillController.cs ---
using UnityEngine;
using System.Linq;

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

        // 1. 从所有装备中找到核心 (Core)
        var coreSO = data.AllEquippedSOs.FirstOrDefault(c => c.Type == ComponentType.Core);
        if (coreSO != null)
        {
            // 2. 找到对应等级的配置
            // 因为没传 Instance，我们根据总血量倒推(或者从Inventory拿)。
            // 为了安全，我们可以直接让 RuntimeChimeraData 存入确切的 ActiveSkillConfig。
            // 这里用简易版：找到匹配的 SO，取1级测试，实际中建议你在 Assemble 阶段直接把 ActiveSkill 存进 runtimeData！

            // 假设我们在这里通过名字去库存里找对应的 Instance (简单处理)
            var coreInstance = PlayerInventoryManager.Instance.ComponentInventory.FirstOrDefault(c => c.BaseData == coreSO && c.EquippedUnitID == data.UnitID);
            int lv = coreInstance != null ? coreInstance.CurrentLevel : 1;

            var lvData = coreSO.GetLevelData(lv);
            if (lvData != null && lvData.ActiveSkill != null && lvData.ActiveSkill.HasActiveSkill)
            {
                SkillConfig = lvData.ActiveSkill;
                CurrentCooldown = 0f;
            }
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

        // 检查并扣除 CP
        if (GlobalCPManager.Instance != null && !GlobalCPManager.Instance.ModifyCP(-SkillConfig.CPCost))
        {
            Debug.LogWarning($"【指令拒绝】CP 不足，需要 {SkillConfig.CPCost}！");
            return false;
        }

        CurrentCooldown = SkillConfig.Cooldown;

        // 执行 ECA 魔法总线！(极其关键的上下文构建：Self-Cast)
        ECAContext context = new ECAContext
        {
            PrimaryTarget = this.transform,      // 目标是自己
            SourceEntity = this.transform,       // 施法者也是自己
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

    // ==========================================
    // 视觉反馈：悬停高亮
    // ==========================================
    private Color originalColor = Color.clear;
    public void SetHighlight(bool isHighlighted)
    {
        if (IsDead) return;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs)
        {
            // 避开血条等UI
            if (sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;

            if (originalColor == Color.clear) originalColor = sr.color;

            // 高亮方案：如果是高亮，就变成刺眼的亮白色；否则恢复原色
            sr.color = isHighlighted ? new Color(2f, 2f, 2f, 1f) : originalColor;
        }
    }
}