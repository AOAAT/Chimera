// --- START OF FILE Action_PlaySound.cs ---
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlaySound", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 播放音效 (Play Sound)")]
public class Action_PlaySound : ECAAction
{
    [Header("=== 音频池配置 (随机抽取播放) ===")]
    [Tooltip("点击 '+' 号添加多个音效文件，每次触发时会随机挑一个播放！")]
    public List<AudioClip> ClipsToPlay = new List<AudioClip>();

    [Tooltip("如果是暴击，是否替换为专属的暴击音效？(可选)")]
    public List<AudioClip> CriticalClipsOverride = new List<AudioClip>();

    [Header("=== 播放参数 ===")]
    [Range(0f, 2f)] public float Volume = 1.0f;
    [Tooltip("音高随机浮动值 (让机枪连射不单调，推荐 0.1)")]
    [Range(0f, 0.5f)] public float PitchJitter = 0.1f;

    public override void Execute(ECAContext context)
    {
        // 1. 根据是否暴击，选择对应的音频池
        var targetPool = (context.IsCriticalHit && CriticalClipsOverride.Count > 0) ? CriticalClipsOverride : ClipsToPlay;

        // 2. 如果池子是空的，直接返回防报错
        if (targetPool == null || targetPool.Count == 0) return;

        // 3. 从池子里随机抽一个音效！
        AudioClip finalClip = targetPool[Random.Range(0, targetPool.Count)];
        if (finalClip == null) return;

        // 4. 呼叫全局对象池播放 (ImpactPoint 是精准的枪口或受击点)
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.PlaySound(finalClip, context.ImpactPoint, Volume, PitchJitter);
        }
    }
}