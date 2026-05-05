
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("=== 音乐库配置 ===")]
    public AudioClip BGM_Hangar;
    public AudioClip BGM_Map;
    public AudioClip BGM_Combat;
    public AudioClip BGM_Erosion;

    [Header("=== 性能参数 ===")]
    public float FadeDuration = 1.5f; // 切歌时的过渡时长
    public float MaxVolume = 0.6f;

    private AudioSource sourceA;
    private AudioSource nextSource;
    private MusicState currentState = MusicState.Silence;
    private AudioLowPassFilter lowPassFilter;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        // 初始化双通道播放器
        sourceA = gameObject.AddComponent<AudioSource>();
        nextSource = gameObject.AddComponent<AudioSource>();
        sourceA.loop = nextSource.loop = true;
        sourceA.playOnAwake = nextSource.playOnAwake = false;

        // 初始化低通滤波器（用于入舱感）
        lowPassFilter = gameObject.GetComponent<AudioLowPassFilter>();
        if (lowPassFilter == null) lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        lowPassFilter.enabled = false; // 初始关闭
    }

    // ==========================================
    // 核心接口：一键切换情绪状态
    // ==========================================
    public void SwitchState(MusicState newState)
    {
        if (newState == currentState) return;

        currentState = newState;
        AudioClip targetClip = GetClipForState(newState);

        // 如果素材缺失，平滑淡出当前音乐并停止
        if (targetClip == null)
        {
            Debug.LogWarning($"【BGM警告】状态 {newState} 缺少音频素材，将进入静默。");
            StartCoroutine(FadeToSilence());
            return;
        }

        // 启动平滑切歌协程
        StopAllCoroutines();
        StartCoroutine(CrossFadeRoutine(targetClip));
    }

    // ==========================================
    // 沉浸感控制：开启/关闭“入舱滤波”
    // ==========================================
    public void SetImmersionMode(bool isInsideCabin)
    {
        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = isInsideCabin;
            lowPassFilter.cutoffFrequency = 800f; // 设置为较低频率，营造闷声感
            // Debug.Log($"<color=#00FFFF>【音响控制】</color> 入舱模式: {isInsideCabin}");
        }
    }

    private AudioClip GetClipForState(MusicState state)
    {
        switch (state)
        {
            case MusicState.Hangar: return BGM_Hangar;
            case MusicState.Map: return BGM_Map;
            case MusicState.Combat: return BGM_Combat;
            case MusicState.Event_Erosion: return BGM_Erosion;
            default: return null;
        }
    }

    private IEnumerator CrossFadeRoutine(AudioClip newClip)
    {
        // 1. 将新曲目加载到闲置的通道
        nextSource.clip = newClip;
        nextSource.volume = 0;
        nextSource.Play();

        float timer = 0;
        float startVolA = sourceA.volume;

        // 2. 音量交叉对冲
        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float percent = timer / FadeDuration;

            sourceA.volume = Mathf.Lerp(startVolA, 0, percent);
            nextSource.volume = Mathf.Lerp(0, MaxVolume, percent);
            yield return null;
        }

        // 3. 交换通道身份
        sourceA.Stop();
        AudioSource temp = sourceA;
        sourceA = nextSource;
        nextSource = temp;
    }

    private IEnumerator FadeToSilence()
    {
        float startVol = sourceA.volume;
        float timer = 0;
        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            sourceA.volume = Mathf.Lerp(startVol, 0, timer / FadeDuration);
            yield return null;
        }
        sourceA.Stop();
    }
}