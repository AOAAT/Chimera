using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("=== 基础音乐库 ===")]
    public AudioClip BGM_MainMenu;     // 👈 统一命名规范
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

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioLowPassFilter filterA;
    private AudioLowPassFilter filterB;

    private AudioClip currentlyPlayingClip;
    private float targetFrequency = 22000f;
    private bool isSourceATarget = true;

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
            return; // 👈 增加 return，防止销毁中的物体继续跑逻辑
        }

        // 初始化物理隔离通道
        sourceA = CreateChannel("MusicChannel_A", out filterA);
        sourceB = CreateChannel("MusicChannel_B", out filterB);

        targetFrequency = NormalFrequency;
    }

    private AudioSource CreateChannel(string name, out AudioLowPassFilter filter)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(this.transform);

        AudioSource s = go.AddComponent<AudioSource>();
        filter = go.AddComponent<AudioLowPassFilter>();

        s.playOnAwake = false;
        s.loop = true;
        s.priority = 0;
        s.spatialBlend = 0f;
        s.volume = 0f;

        filter.cutoffFrequency = 22000f;
        return s;
    }

    private void Update()
    {
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
        // 关键判定：如果目标音乐和正在播的一样，直接拦截，防止重复触发淡入淡出
        if (target == currentlyPlayingClip) return;
        currentlyPlayingClip = target;

        if (target == null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeToSilence());
            return;
        }

        StopAllCoroutines();
        StartCoroutine(CrossFadeRoutine(target));
    }

    private IEnumerator CrossFadeRoutine(AudioClip newClip)
    {
        // 1. 确定进入和退出的通道
        AudioSource activeSource = isSourceATarget ? sourceA : sourceB;
        AudioSource inactiveSource = isSourceATarget ? sourceB : sourceA;

        activeSource.clip = newClip;
        activeSource.volume = 0;
        activeSource.Play();

        float timer = 0;
        float startVolInactive = inactiveSource.volume;

        // 2. 双通道交叉淡入淡出
        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime; // 👈 使用 unscaled，确保暂停时音乐过渡也不卡顿
            float p = timer / FadeDuration;

            activeSource.volume = Mathf.Lerp(0, MaxVolume, p);
            inactiveSource.volume = Mathf.Lerp(startVolInactive, 0, p);
            yield return null;
        }

        // 3. 收尾
        activeSource.volume = MaxVolume;
        inactiveSource.Stop();
        inactiveSource.clip = null;
        inactiveSource.volume = 0;

        isSourceATarget = !isSourceATarget; // 身份轮换
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

    // --- 👇【核心修复点】：补全主菜单状态映射 ---
    private AudioClip GetDefaultClip(MusicState state)
    {
        switch (state)
        {
            case MusicState.MainMenu: return BGM_MainMenu; // 👈 增加这一行
            case MusicState.Map: return BGM_Map;
            case MusicState.Combat: return BGM_Combat;
            case MusicState.Shop: return BGM_Shop;
            case MusicState.Event: return BGM_Event_Default;
            case MusicState.Loot: return BGM_Loot;
            default: return null;
        }
    }
}