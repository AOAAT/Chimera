using System.Collections.Generic;
using UnityEngine;

public class GlobalAudioManager : MonoBehaviour
{
    public static GlobalAudioManager Instance;

    [Header("=== UI 音效索引库 ===")]
    public UISoundAtlasSO UIAtlas;

    [Header("=== 对象池配置 ===")]
    public int PoolSize = 20;
    private Queue<AudioSource> audioPool = new Queue<AudioSource>();

    private Dictionary<UISoundType, float> lastPlayedTime = new Dictionary<UISoundType, float>();
    private const float MIN_SFX_INTERVAL = 0.08f; // 两次同类音效之间的最小间隔（秒）
    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        InitializePool();
    }

    private void InitializePool()
    {
        GameObject poolRoot = new GameObject("[AudioPool_Final]");
        poolRoot.transform.SetParent(this.transform);

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject obj = new GameObject($"Source_{i}");
            obj.transform.SetParent(poolRoot.transform);
            AudioSource s = obj.AddComponent<AudioSource>();
            s.playOnAwake = false;
            obj.SetActive(false);
            audioPool.Enqueue(s);
        }
    }

    // ==========================================
    // 1. UI 系统接口：播放预设好的类型
    // ==========================================
    public void PlayUISound(UISoundType type)
    {
        if (UIAtlas == null) return;

        // --- 👇【核心优化：防抖逻辑】---
        // 如果是悬停类音效，进行高频拦截
        if (type == UISoundType.Generic_Hover)
        {
            if (lastPlayedTime.ContainsKey(type) && (Time.unscaledTime - lastPlayedTime[type]) < MIN_SFX_INTERVAL)
            {
                return; // 还没到 CD，直接拦截，保护耳朵和 BGM
            }
            lastPlayedTime[type] = Time.unscaledTime;
        }
        // ----------------------------------

        AudioProfileSO profile = UIAtlas.GetProfile(type);
        if (profile != null) PlayProfile(profile, Vector3.zero, true);
    }
    public void PlayProfile(AudioProfileSO profile, Vector3 position, bool isUI = false)
    {
        if (profile == null || audioPool.Count == 0) return;

        AudioSource source = audioPool.Dequeue();
        source.gameObject.SetActive(true);
        source.transform.position = position;
        source.spatialBlend = isUI ? 0f : 0.5f;

        profile.Play(source);
        StartCoroutine(ReturnToPool(source, 2.0f));
    }

    // ==========================================
    // 2. 战斗 ECA 接口：补回缺失的直接播放方法
    // ==========================================
    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, float pitchJitter = 0.1f)
    {
        if (clip == null || audioPool.Count == 0) return;

        AudioSource source = audioPool.Dequeue();
        source.gameObject.SetActive(true);
        source.transform.position = position;
        source.spatialBlend = 0.5f; // 战斗音效默认带 3D 距离感

        source.clip = clip;
        source.volume = volume;
        source.pitch = 1.0f + Random.Range(-pitchJitter, pitchJitter);
        source.Play();

        StartCoroutine(ReturnToPool(source, clip.length + 0.1f));
    }

    private System.Collections.IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        source.Stop();
        source.gameObject.SetActive(false);
        audioPool.Enqueue(source);
    }
}