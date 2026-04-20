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
        if (BuffToApply == null) return;

        if (TargetMode == BuffTargetMode.Single)
        {
            // --- 单体模式 ---
            if (context.PrimaryTarget != null)
            {
                Apply(context.PrimaryTarget.GetComponentInParent<BuffManager>(), context);
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

        // 2. 筛选目标池
        List<DamageReceiver> potentialTargets = new List<DamageReceiver>();

        bool sourceIsEnemy = context.IsEnemyFire;

        if (FactionFilter == BuffFactionFilter.Enemies || FactionFilter == BuffFactionFilter.Both)
        {
            // 如果来源是怪，敌人就是玩家；如果来源是玩家，敌人就是怪
            var enemies = sourceIsEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
            potentialTargets.AddRange(enemies);
        }

        if (FactionFilter == BuffFactionFilter.Allies || FactionFilter == BuffFactionFilter.Both)
        {
            var allies = sourceIsEnemy ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
            potentialTargets.AddRange(allies);
        }

        // 3. 距离过滤并施加
        foreach (var t in potentialTargets)
        {
            if (t == null || t.CurrentHP <= 0) continue;

            if (Vector3.Distance(center, t.transform.position) <= scaledRadius)
            {
                Apply(t.GetComponent<BuffManager>(), context);
            }
        }
    }

    private void Apply(BuffManager mgr, ECAContext context)
    {
        if (mgr == null) return;
        for (int i = 0; i < Stacks; i++)
        {
            mgr.ApplyBuff(BuffToApply, context);
        }
    }
}