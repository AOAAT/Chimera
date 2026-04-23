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

        // 【核心修复】：根据 context 识别谁才是真正的敌人
        // 如果 context.IsEnemyFire 为 true，说明发动者是精英怪，对手就是 PlayerUnits
        // 如果 context.IsEnemyFire 为 false，说明发动者是玩家，对手就是 Enemies
        var opponentList = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        var enemies = opponentList.Where(e => e != null && e.CurrentHP > 0).ToList();

        if (enemies.Count == 0) return;

        // 随机抽一个倒霉蛋（对手阵营的人）
        DamageReceiver target = enemies[Random.Range(0, enemies.Count)];
        Vector2 dashDir = (target.transform.position - context.SourceEntity.position).normalized;

        // 执行物理冲刺
        myAI.ExecuteDash(dashDir, SpeedMultiplier, Duration);

        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.3f, 0.2f);
    }
}