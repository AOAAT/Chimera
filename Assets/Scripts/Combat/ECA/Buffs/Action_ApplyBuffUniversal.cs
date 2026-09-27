// --- Action_ApplyBuffUniversal.cs (带诊断日志版) ---
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum BuffTargetMode { Single, Area }
public enum BuffAreaCenter { ImpactPoint, SourceSelf }
public enum BuffFactionFilter { Enemies, Allies, Both }

[CreateAssetMenu(fileName = "ApplyBuffUniversal", menuName = "Chimera Protocol/2. ECA 机制积木/状态 - 万能 Buff 施加器")]
public class Action_ApplyBuffUniversal : ECAAction
{
    [Header("=== 1. 基础配置 ===")]
    public BuffDataSO BuffToApply;
    [Tooltip("单次执行施加的层数")]
    public int Stacks = 1;

    [Header("=== 2. 目标维度 ===")]
    public BuffTargetMode TargetMode = BuffTargetMode.Single;

    [Header("=== 3. 范围配置 (仅在 Area 模式下有效) ===")]
    public BuffAreaCenter CenterType = BuffAreaCenter.ImpactPoint;
    public BuffFactionFilter FactionFilter = BuffFactionFilter.Enemies;
    public float Radius = 5f;

    public override void Execute(ECAContext context)
    {
        // 🌟 日志 1：进入积木执行
        Debug.Log($"<color=#00FFFF>【万能Buff-启动】</color> 正在运行积木: {this.name} | 模式: {TargetMode}");

        if (BuffToApply == null)
        {
            Debug.LogWarning($"<color=red>【万能Buff-警告】</color> 积木 {this.name} 未配置 BuffToApply 数据！");
            return;
        }

        if (TargetMode == BuffTargetMode.Single)
        {
            // --- 单体模式 ---
            if (context.PrimaryTarget != null)
            {
                Debug.Log($"<color=white>  -> 准备对单体目标 [{context.PrimaryTarget.name}] 进行施加</color>");
                Apply(context.PrimaryTarget.GetComponentInParent<BuffManager>(), context);
            }
            else
            {
                Debug.LogWarning($"<color=yellow>【万能Buff-中断】</color> 单体模式下 context.PrimaryTarget 为空！");
            }
        }
        else
        {
            // --- 范围模式 ---
            ExecuteAreaEffect(context);
        }
    }

    private void ExecuteAreaEffect(ECAContext context)
    {
        // 1. 确定中心点
        Vector3 center = (CenterType == BuffAreaCenter.ImpactPoint) ? context.ImpactPoint : context.SourceEntity.position;
        float scaledRadius = CombatSandbox.GetDist(Radius);

        Debug.Log($"<color=white>  -> 开启区域搜索: 中心点={CenterType}, 半径={scaledRadius}</color>");

        // 2. 筛选目标池
        List<DamageReceiver> potentialTargets = new List<DamageReceiver>();
        bool sourceIsEnemy = context.IsEnemyFire;

        if (FactionFilter == BuffFactionFilter.Enemies || FactionFilter == BuffFactionFilter.Both)
        {
            var enemies = sourceIsEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
            potentialTargets.AddRange(enemies);
        }

        if (FactionFilter == BuffFactionFilter.Allies || FactionFilter == BuffFactionFilter.Both)
        {
            var allies = sourceIsEnemy ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
            potentialTargets.AddRange(allies);
        }

        Debug.Log($"<color=grey>  -> 目标池初步筛选完成，候选人数: {potentialTargets.Count}</color>");

        int affectedCount = 0;
        // 3. 距离过滤并施加
        foreach (var t in potentialTargets)
        {
            if (t == null || t.CurrentHP <= 0) continue;

            if (Vector3.Distance(center, t.transform.position) <= scaledRadius)
            {
                Apply(t.GetComponent<BuffManager>(), context);
                affectedCount++;
            }
        }

        Debug.Log($"<color=#00FF00>  -> 区域施加完毕，共计波及 {affectedCount} 个目标</color>");
    }

    private void Apply(BuffManager mgr, ECAContext context)
    {
        if (mgr == null)
        {
            // 如果没找到 BuffManager，尝试在父级和子级搜索（加固兼容性）
            if (context.PrimaryTarget != null)
                mgr = context.PrimaryTarget.GetComponentInChildren<BuffManager>();

            if (mgr == null)
            {
                string targetName = context.PrimaryTarget != null ? context.PrimaryTarget.name : "未知";
                Debug.LogError($"<color=red>【施加失败】</color> 目标 [{targetName}] 身上缺少 BuffManager 组件！");
                return;
            }
        }

        // 正式施加
        for (int i = 0; i < Stacks; i++)
        {
            mgr.ApplyBuff(BuffToApply, context);
        }

        Debug.Log($"<color=#FF00FF>【成功】</color> 已向 [{mgr.gameObject.name}] 注入 Buff: {BuffToApply.BuffName} (共{Stacks}层)");
    }
}