// --- START OF FILE Action_ScreenEffect.cs ---
using UnityEngine;

public enum ScreenEffectTriggerCondition
{
    Always,             // 只要执行就触发
    OnlyOnCritical,     // 仅在发生暴击时触发 (适合重型狙击枪)
    OnlyOnPlayerHit     // 仅在玩家受伤时触发 (适合怪物攻击)
}

[CreateAssetMenu(fileName = "ScreenEffect", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 屏幕震动与闪烁 (Screen Effect)")]
public class Action_ScreenEffect : ECAAction
{
    [Header("=== 触发条件 ===")]
    public ScreenEffectTriggerCondition Condition = ScreenEffectTriggerCondition.Always;

    [Header("=== 镜头震动 (Camera Shake) ===")]
    public bool EnableShake = true;
    [Range(0f, 2f)] public float ShakeIntensity = 0.2f;
    [Range(0.1f, 1f)] public float ShakeDuration = 0.2f;

    [Header("=== 屏幕闪烁 (Screen Flash) ===")]
    public bool EnableFlash = false;
    public Color FlashColor = new Color(1f, 0f, 0f, 0.4f); // 默认半透明红色
    [Range(0.1f, 1f)] public float FlashDuration = 0.3f;

    public override void Execute(ECAContext context)
    {
        // 1. 过滤条件
        if (Condition == ScreenEffectTriggerCondition.OnlyOnCritical && !context.IsCriticalHit) return;
        if (Condition == ScreenEffectTriggerCondition.OnlyOnPlayerHit && context.IsEnemyFire) return; // 敌人的火力打中了才闪

        if (ScreenEffectManager.Instance == null) return;

        // 2. 触发表现
        if (EnableShake) ScreenEffectManager.Instance.TriggerShake(ShakeIntensity, ShakeDuration);
        if (EnableFlash) ScreenEffectManager.Instance.TriggerFlash(FlashColor, FlashDuration);
    }
}