using UnityEngine;

[CreateAssetMenu(fileName = "TimeFreeze", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 时间冻结 (Hit Stop)")]
public class Action_TimeFreeze : ECAAction
{
    [Header("=== 冻结设定 ===")]
    [Tooltip("冻结持续多久？推荐 0.05 ~ 0.1")]
    public float FreezeDuration = 0.08f;

    [Tooltip("冻结时的速率？推荐 0.01 ~ 0.05")]
    [Range(0f, 0.5f)] public float SlownessScale = 0.02f;

    [Tooltip("是否仅在暴击时触发？")]
    public bool OnlyOnCritical = false;

    public override void Execute(ECAContext context)
    {
        if (OnlyOnCritical && !context.IsCriticalHit) return;

        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.RequestHitStop(FreezeDuration, SlownessScale);
        }
    }
}