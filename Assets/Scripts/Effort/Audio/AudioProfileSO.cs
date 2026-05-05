using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewAudioProfile", menuName = "Chimera Protocol/Audio/Audio Profile")]
public class AudioProfileSO : ScriptableObject
{
    [Header("=== 音频采样池 ===")]
    [Tooltip("随机从池中抽取一段播放，防止听觉疲劳")]
    public List<AudioClip> Clips = new List<AudioClip>();

    [Header("=== 播放参数 ===")]
    [Range(0f, 2f)] public float Volume = 1.0f;

    [Tooltip("基础音高")]
    [Range(0.1f, 3f)] public float BasePitch = 1.0f;

    [Tooltip("随机音高偏移量 (0.1 代表音高在 0.9~1.1 之间浮动)")]
    [Range(0f, 0.5f)] public float PitchJitter = 0.1f;

    public void Play(AudioSource source)
    {
        if (Clips.Count == 0 || source == null) return;

        source.clip = Clips[Random.Range(0, Clips.Count)];
        source.volume = Volume;
        source.pitch = BasePitch + Random.Range(-PitchJitter, PitchJitter);
        source.PlayOneShot(source.clip);
    }
}