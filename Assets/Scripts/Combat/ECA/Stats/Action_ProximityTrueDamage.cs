using UnityEngine;

[CreateAssetMenu(fileName = "ProximityDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 圣音号角(距离缩放)")]
public class Action_ProximityTrueDamage : ECAAction
{
    public float MaxBonusMultiplier = 3.0f;
    public float BaseRange = 5.0f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null || context.SourceEntity == null) return;

        float dist = Vector3.Distance(context.SourceEntity.position, context.PrimaryTarget.position);

        float realBaseRange = CombatSandbox.GetDist(BaseRange); // 👈 关键对齐

        // 距离越近，比率越高
        float ratio = Mathf.Clamp(1.0f + (1.0f - dist / realBaseRange) * (MaxBonusMultiplier - 1.0f), 1f, MaxBonusMultiplier);
        float finalDmg = context.BaseDamage * ratio;

        var receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            receiver.TakeDamage(finalDmg, "圣音号角", true); // 强制真伤
        }
    }
}