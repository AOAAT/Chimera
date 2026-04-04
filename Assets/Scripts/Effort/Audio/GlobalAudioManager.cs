// --- START OF FILE GlobalAudioManager.cs ---
using System.Collections.Generic;
using UnityEngine;

public class GlobalAudioManager : MonoBehaviour
{
    public static GlobalAudioManager Instance;

    [Header("=== 音频对象池配置 ===")]
    public int PoolSize = 15;
    private Queue<AudioSource> audioPool = new Queue<AudioSource>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        InitializePool();
    }

    private void InitializePool()
    {
        GameObject poolRoot = new GameObject("[AudioPool]");
        poolRoot.transform.SetParent(this.transform);

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject obj = new GameObject($"AudioSource_{i}");
            obj.transform.SetParent(poolRoot.transform);

            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.5f; // 50%的 3D 效果，有轻微的近大远小
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 30f;

            obj.SetActive(false);
            audioPool.Enqueue(source);
        }
    }

    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, float pitchJitter = 0.1f)
    {
        if (clip == null || audioPool.Count == 0) return;

        AudioSource source = audioPool.Dequeue();
        source.gameObject.SetActive(true);
        source.transform.position = position;

        source.clip = clip;
        source.volume = volume;
        // 有机浮动：给音高加一点随机变化，让连续播放的同一个音效听起来不一样！
        source.pitch = 1.0f + Random.Range(-pitchJitter, pitchJitter);

        source.Play();

        // 使用协程在播放结束后自动回收
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    private System.Collections.IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
        source.gameObject.SetActive(false);
        audioPool.Enqueue(source);
    }
}