// --- Action_ElephantRam.cs (诊断加固版) ---
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "ElephantRam_V2", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 大象蛮力冲撞 V2")]
public class Action_ElephantRam : ECAAction
{
    [Header("=== 冲撞性能 ===")]
    public float SpeedMultiplier = 4.0f;
    public float Duration = 0.6f;

    public Action_ElephantRam() { Priority = 200; }

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 1. 获取 AI 控制器 (加固：支持从子物体向上查找)
        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI == null) myAI = context.SourceEntity.GetComponentInParent<ChimeraAIController>();

        if (myAI == null)
        {
            Debug.LogError($"<color=red>【大象腿错误】</color> {context.SourceEntity.name} 身上没找到 ChimeraAIController！");
            return;
        }

        // 2. 识别敌手阵营
        var opponentList = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        var enemies = opponentList.Where(e => e != null && e.CurrentHP > 0).ToList();

        Vector2 dashDir = Vector2.zero;

        if (enemies.Count > 0)
        {
            // --- 逻辑 A：有敌人，瞄准冲撞 ---
            DamageReceiver target = enemies[Random.Range(0, enemies.Count)];
            dashDir = (target.transform.position - context.SourceEntity.position).normalized;
            Debug.Log($"<color=#FFD700>【大象腿】</color> 瞄准了敌人 {target.name}，发动冲撞！");
        }
        else
        {
            // --- 逻辑 B：没敌人，尝试朝着“面朝方向”或“随机方向”空冲 (用于测试反馈) ---
            Rigidbody2D rb = context.SourceEntity.GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.1f)
                dashDir = rb.velocity.normalized;
            else
                dashDir = Random.insideUnitCircle.normalized;

            Debug.LogWarning($"<color=orange>【大象腿警告】</color> 视野内没有活着的敌人，执行随机方向空冲以示示范。");
        }

        // 3. 执行物理冲刺
        myAI.ExecuteDash(dashDir, SpeedMultiplier, Duration);

        // 4. 视觉震动
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.3f, 0.2f);
    }
}