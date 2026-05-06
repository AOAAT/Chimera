
using UnityEngine;
using System.Collections;

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
    public float FadeDuration = 1.2f;
    [Range(0f, 1f)] public float MaxVolume = 0.6f;

    [Header("=== 沉浸感缓动设置 ===")]
    public float NormalFrequency = 22000f;
    public float MuffledFrequency = 800f;
    public float FilterSmoothSpeed = 8f;

    private AudioSource sourceA;
    private AudioSource nextSource;
    private AudioClip currentlyPlayingClip;
    private AudioLowPassFilter lowPassFilter;
    private float targetFrequency = 22000f;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        sourceA = gameObject.AddComponent<AudioSource>();
        nextSource = gameObject.AddComponent<AudioSource>();
        sourceA.loop = nextSource.loop = true;
        sourceA.priority = 0;
        nextSource.priority = 0;

        lowPassFilter = GetComponent<AudioLowPassFilter>() ?? gameObject.AddComponent<AudioLowPassFilter>();
        lowPassFilter.enabled = true;
        lowPassFilter.cutoffFrequency = NormalFrequency;
        targetFrequency = NormalFrequency;
    }

    private void Update()
    {
        if (lowPassFilter != null)
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFrequency, Time.unscaledDeltaTime * FilterSmoothSpeed);
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
        if (target == null) { StartCoroutine(FadeToSilence()); return; }
        StopAllCoroutines();
        StartCoroutine(CrossFadeRoutine(target));
    }

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

    private IEnumerator CrossFadeRoutine(AudioClip newClip)
    {
        nextSource.clip = newClip;
        nextSource.volume = 0;
        nextSource.Play();
        float timer = 0;
        float startVolA = sourceA.volume;
        while (timer < FadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float p = timer / FadeDuration;
            sourceA.volume = Mathf.Lerp(startVolA, 0, p);
            nextSource.volume = Mathf.Lerp(0, MaxVolume, p);
            yield return null;
        }
        sourceA.Stop();
        AudioSource temp = sourceA; sourceA = nextSource; nextSource = temp;
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
        currentlyPlayingClip = null;
    }

    public void SetImmersionMode(bool on) { targetFrequency = on ? MuffledFrequency : NormalFrequency; }
}