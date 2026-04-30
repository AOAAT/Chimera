using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum TeleportTargetMode { NearestOpponent, RandomOpponent, RandomAlly }

[CreateAssetMenu(fileName = "Act_TeleportNear", menuName = "Chimera Protocol/2. ECA 机制积木/战术 - 瞬间闪烁(修复版)")]
public class Action_TeleportNearTarget : ECAAction
{
    public TeleportTargetMode TargetMode;
    public float DistanceFromTarget = 1.2f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        DamageReceiver myDR = context.SourceEntity.GetComponent<DamageReceiver>();
        bool iAmEnemy = myDR != null ? myDR.isEnemy : true;

        Transform finalTarget = null;

        // 1. 👇【新增】：多样化的目标搜索逻辑
        if (TargetMode == TeleportTargetMode.NearestOpponent)
        {
            var opponents = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
            finalTarget = opponents.Where(o => o != null && o.CurrentHP > 0)
                .OrderBy(o => Vector2.Distance(context.SourceEntity.position, o.transform.position))
                .FirstOrDefault()?.transform;
        }
        else if (TargetMode == TeleportTargetMode.RandomOpponent)
        {
            var opponents = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
            var validOnes = opponents.Where(o => o != null && o.CurrentHP > 0).ToList();
            if (validOnes.Count > 0) finalTarget = validOnes[Random.Range(0, validOnes.Count)].transform;
        }
        else if (TargetMode == TeleportTargetMode.RandomAlly)
        {
            var allies = iAmEnemy ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
            var validOnes = allies.Where(a => a != null && a.CurrentHP > 0 && a.transform != context.SourceEntity).ToList();
            if (validOnes.Count > 0) finalTarget = validOnes[Random.Range(0, validOnes.Count)].transform;
        }

        if (finalTarget == null) return;

        // 2. 计算落点
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float realDist = CombatSandbox.GetDist(DistanceFromTarget);
        Vector3 landingPos = finalTarget.position + (Vector3)(randomDir * realDist);

        // 3. 👇【关键修复】：仅移动坐标，不修改 Rotation！
        // 这样可以保证原来的物理 BoxCollider2D Offset 依然指向脚底，贴图不会翻转。
        context.SourceEntity.position = landingPos;

        // 4. 重置刚体速度，防止带着之前的惯性飞出去
        Rigidbody2D rb = context.SourceEntity.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;

        Debug.Log($"<color=#00FF00>【跳出维度】</color> {context.SourceEntity.name} 已闪烁至 {finalTarget.name} 附近");
    }
}