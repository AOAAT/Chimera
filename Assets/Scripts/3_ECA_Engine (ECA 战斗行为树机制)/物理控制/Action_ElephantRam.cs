using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "ElephantRam", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 大象蛮力冲撞")]
public class Action_ElephantRam : ECAAction
{
    [Header("=== 冲撞性能 ===")]
    public float SpeedMultiplier = 4.0f;
    public float Duration = 0.6f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        ChimeraAIController myAI = context.SourceEntity.GetComponent<ChimeraAIController>();
        if (myAI == null) return;
        var potentialTargets = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        // 1. 寻找随机目标 (符合“针对随机敌人”的描述)
        var enemies = CombatDirector.ActiveEnemies.Where(e => e != null && e.CurrentHP > 0).ToList();

        if (enemies.Count == 0) return;

        // 随机抽一个倒霉蛋
        DamageReceiver target = enemies[Random.Range(0, enemies.Count)];
        Vector2 dashDir = (target.transform.position - context.SourceEntity.position).normalized;

        // 2. 执行物理冲刺
        // 使用我们之前加固过的 ExecuteDash，它自带“贴脸爆破”判定
        myAI.ExecuteDash(dashDir, SpeedMultiplier, Duration);

        // 3. 视觉反馈
        Debug.Log($"<color=#FFFFFF>【大象腿】</color> 锁定了随机目标 {target.name}，发起泥头车冲撞！");

        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.3f, 0.2f);
    }
}