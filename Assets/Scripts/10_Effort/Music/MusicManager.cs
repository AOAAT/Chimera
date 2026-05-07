using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("=== 基础音乐库 ===")]
    public AudioClip BGM_Map;
    public AudioClip BGM_Combat;
    public AudioClip BGM_Shop;
    public AudioClip BGM_Event_Default;
    public AudioClip BGM_Loot;

    [Header("=== 播放参数 ===")]
    public float FadeDuration = 1.5f;
    [Range(0f, 1f)] public float MaxVolume = 0.6f;

    [Header("=== 沉浸感缓动设置 ===")]
    public float NormalFrequency = 22000f;
    public float MuffledFrequency = 800f;
    public float FilterSmoothSpeed = 8f;

    // 🌟 核心改动：不再直接引用 Source，而是管理两个子通道
    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioLowPassFilter filterA;
    private AudioLowPassFilter filterB;

    private AudioClip currentlyPlayingClip;
    private float targetFrequency = 22000f;
    private bool isSourceATarget = true; // 标记当前哪一个是主播放通道

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        // 🌟【核心修复】：物理隔离初始化
        // 我们在物体下方动态创建两个纯净的子物体来承载音频
        sourceA = CreateChannel("MusicChannel_A", out filterA);
        sourceB = CreateChannel("MusicChannel_B", out filterB);

        targetFrequency = NormalFrequency;
    }

    // 辅助方法：创建一个带有 Source 和 Filter 的干净通道
    private AudioSource CreateChannel(string name, out AudioLowPassFilter filter)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(this.transform);

        // 顺序极其关键：先加 Source，再加 Filter，绝不报错
        AudioSource s = go.AddComponent<AudioSource>();
        filter = go.AddComponent<AudioLowPassFilter>();

        s.playOnAwake = false;
        s.loop = true;
        s.priority = 0; // 最高优先级
        s.spatialBlend = 0f;
        s.volume = 0f;

        filter.cutoffFrequency = 22000f;
        return s;
    }

    private void Update()
    {
        // 平滑同步两个通道的滤波器频率
        if (filterA != null && filterB != null)
        {
            float currentFreq = Mathf.Lerp(filterA.cutoffFrequency, targetFrequency, Time.unscaledDeltaTime * FilterSmoothSpeed);
            filterA.cutoffFrequency = currentFreq;
            filterB.cutoffFrequency = currentFreq;
        }
    }

    public void SwitchState(MusicState newState)
    {
        AudioClip target = GetDefaultClip(newState);
        ExecuteTransition(target);
    }

    public void PlayEventMusic(AudioClip overrideClip)
    {
        AudioClip target = (overrideClip != null) ? overrideClip : BGM_Event_Default;
        ExecuteTransition(target);
    }

    private void ExecuteTransition(AudioClip target)
    {
        if (target == currentlyPlayingClip) return;
        currentlyPlayingClip = target;

        if (target == null) { StopAllCoroutines(); StartCoroutine(FadeToSilence()); return; }

        StopAllCoroutines();
        StartCoroutine(CrossFadeRoutine(target));
    }

    private IEnumerator CrossFadeRoutine(AudioClip newClip)
    {
        // 确定谁是进入通道，谁是退出通道
        AudioSource activeSource = isSourceATarget ? sourceA : sourceB;
        AudioSource inactiveSource = isSourceATarget ? sourceB : sourceA;

        activeSource.clip = newClip;
        activeSource.volume = 0;
        activeSource.Play();

        float timer = 0;
        float startVolInactive = inactiveSource.volume;

        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float p = timer / FadeDuration;

            activeSource.volume = Mathf.Lerp(0, MaxVolume, p);
            inactiveSource.volume = Mathf.Lerp(startVolInactive, 0, p);
            yield return null;
        }

        inactiveSource.Stop();
        inactiveSource.clip = null;
        isSourceATarget = !isSourceATarget; // 身份切换
    }

    private IEnumerator FadeToSilence()
    {
        float timer = 0;
        float volA = sourceA.volume;
        float volB = sourceB.volume;

        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float p = timer / FadeDuration;
            sourceA.volume = Mathf.Lerp(volA, 0, p);
            sourceB.volume = Mathf.Lerp(volB, 0, p);
            yield return null;
        }
        sourceA.Stop(); sourceB.Stop();
    }

    public void SetImmersionMode(bool on) { targetFrequency = on ? MuffledFrequency : NormalFrequency; }

    private AudioClip GetDefaultClip(MusicState state)
    {
        switch (state)
        {
            case MusicState.Map: return BGM_Map;
            case MusicState.Combat: return BGM_Combat;
            case MusicState.Shop: return BGM_Shop;
            case MusicState.Event: return BGM_Event_Default;
            case MusicState.Loot: return BGM_Loot;
            default: return null;
        }
    }
}