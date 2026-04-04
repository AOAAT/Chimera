// --- START OF FILE Action_PlaySound.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "PlaySound", menuName = "Chimera Protocol/ECA Actions/Feedback: Play Sound (播放音效)")]
public class Action_PlaySound : ECAAction
{
    [Header("=== 音频资产 ===")]
    public AudioClip ClipToPlay;

    [Tooltip("如果是暴击，是否替换为更爽的音效？(可选)")]
    public AudioClip CriticalClipOverride;

    [Header("=== 播放参数 ===")]
    [Range(0f, 2f)] public float Volume = 1.0f;
    [Tooltip("音高随机浮动值(让机枪连射不单调)")]
    [Range(0f, 0.5f)] public float PitchJitter = 0.1f;

    public override void Execute(ECAContext context)
    {
        AudioClip finalClip = (context.IsCriticalHit && CriticalClipOverride != null) ? CriticalClipOverride : ClipToPlay;
        if (finalClip == null) return;

        if (GlobalAudioManager.Instance != null)
        {
            // 因为我们在 WeaponModule 里做过处理：
            // OnFire 时，ImpactPoint 就是枪口位置；OnHit 时，ImpactPoint 就是击中位置。
            // 所以直接用 context.ImpactPoint 极其完美！
            GlobalAudioManager.Instance.PlaySound(finalClip, context.ImpactPoint, Volume, PitchJitter);
        }
    }
}