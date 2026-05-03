using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum DashTargetMode
{
    CurrentTarget,      // 当前锁定的目标
    NearestOpponent,    // 最近的对手
    RandomOpponent,     // 随机一个对手
    RandomDirection,    // 随机方向
    MaintainForward     // 维持当前朝向
}

[CreateAssetMenu(fileName = "Act_UniversalDash", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 万能位移冲刺")]
public class Action_KineticDash : ECAAction
{
    [Header("=== 目标选择 ===")]
    public DashTargetMode TargetMode = DashTargetMode.CurrentTarget;

    [Tooltip("是否为躲避/撤退？(勾选后，冲刺方向会与目标方向相反)")]
    public bool IsDefensive = false;

    [Header("=== 物理性能 ===")]
    public float SpeedMultiplier = 5.0f;
    public float Duration = 0.5f;

    [Header("=== 碰撞伤害 (牛牛专用) ===")]
    [Tooltip("如果是纯位移/躲避，请设为 0")]
    public float DamageConversionRate = 0f;

    [Header("=== 稳定性控制 ===")]
    [Tooltip("如果勾选，冲刺期间免疫硬直和打断（霸体状态）")]
    public bool IsUnstoppable = true;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 1. 确定冲刺向量 (Direction)
        Vector2 dashDir = CalculateDashDirection(context);

        // 2. 动态挂载/更新拦截器 (仅在需要伤害时)
        if (DamageConversionRate > 0.1f)
        {
            var handler = context.SourceEntity.GetComponent<KineticCollisionHandler>();
            if (handler != null) Destroy(handler);
            handler = context.SourceEntity.gameObject.AddComponent<KineticCollisionHandler>();
            handler.Initialize(DamageConversionRate, context.SourceWeapon.OnHitActions, context.SourceWeapon);
            handler.Initialize(DamageConversionRate, context.SourceWeapon.OnHitActions, context.SourceWeapon);
            handler.IsUnstoppable = IsUnstoppable;
            // 开启定时清理
            CombatDirector.Instance.StartCoroutine(CleanupKineticHandler(handler, Duration));
        }

        // 3. 执行物理位移分流
        ExecuteMove(context, dashDir);
    }

    private Vector2 CalculateDashDirection(ECAContext context)
    {
        Transform self = context.SourceEntity;
        Transform target = null;

        // --- A. 寻找参考目标 ---
        switch (TargetMode)
        {
            case DashTargetMode.CurrentTarget:
                target = context.PrimaryTarget;
                break;

            case DashTargetMode.NearestOpponent:
                var opponents = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
                target = opponents.Where(o => o != null && o.CurrentHP > 0)
                    .OrderBy(o => Vector2.Distance(self.position, o.transform.position))
                    .FirstOrDefault()?.transform;
                break;

            case DashTargetMode.RandomOpponent:
                var pool = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
                var valid = pool.Where(o => o != null && o.CurrentHP > 0).ToList();
                if (valid.Count > 0) target = valid[Random.Range(0, valid.Count)].transform;
                break;
        }

        // --- B. 计算基础向量 ---
        Vector2 finalDir = self.right; // 默认保底

        if (TargetMode == DashTargetMode.RandomDirection)
        {
            finalDir = Random.insideUnitCircle.normalized;
        }
        else if (target != null)
        {
            finalDir = (target.position - self.position).normalized;
            if (IsDefensive) finalDir = -finalDir; // 撤退模式：反向
        }
        else if (TargetMode == DashTargetMode.MaintainForward)
        {
            Rigidbody2D rb = self.GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.1f) finalDir = rb.velocity.normalized;
        }

        return finalDir;
    }

    private void ExecuteMove(ECAContext context, Vector2 dir)
    {
        var self = context.SourceEntity;
        var chimeraAI = self.GetComponent<ChimeraAIController>();

        if (chimeraAI != null)
        {
            // 模式 A：玩家/拼装精英
            chimeraAI.ExecuteDash(dir, SpeedMultiplier, Duration);
        }
        else
        {
            // 模式 B：普通单位
            Rigidbody2D rb = self.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float baseSpeed = 3.0f;
                EnemyBrain brain = self.GetComponent<EnemyBrain>();
                if (brain != null) baseSpeed = brain.MyData.GetStat(StatType.MoveSpeed);

                Vector2 finalVel = dir * baseSpeed * SpeedMultiplier;

                // 如果是伤害冲刺，需要 Handler 维持速度
                var handler = self.GetComponent<KineticCollisionHandler>();
                if (handler != null)
                {
                    handler.StartPhysicalDash(finalVel);
                }
                else
                {
                    // 如果是纯躲避，直接给一个瞬时冲力即可，大脑的寻路会在下一帧或稍后接管
                    rb.velocity = finalVel;
                    // 利用 Stagger 机制防止 AI 立即打断冲刺
                    if (brain != null) brain.ApplyImpulse(Vector2.zero, 0.1f);
                }
            }
        }
    }

    private IEnumerator CleanupKineticHandler(KineticCollisionHandler h, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (h != null) h.Shutdown();
    }
}