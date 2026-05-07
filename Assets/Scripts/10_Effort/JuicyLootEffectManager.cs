using UnityEngine;

public class JuicyLootEffectManager : MonoBehaviour
{
    public static JuicyLootEffectManager Instance;

    [Header("=== 配置 ===")]
    public GameObject FlyPrefab; // 一个带有 Image 和 LootFlyEffect 脚本的预制体
    public RectTransform InventoryTarget; // 你手动设置的终点坐标物体
    [Header("=== 废料配置 ===")]
    public GameObject ScrapPrefab;      // 颗粒预制体 (小齿轮/螺丝图标)
    public Sprite[] ScrapSprites;       // 几种不同的废料随机贴图
    public RectTransform ScrapHUDTarget; // 顶部资源栏“废料”图标的坐标
    private void Awake() => Instance = this;

    public void SpawnFlyEffect(Sprite icon, Vector3 startWorldPos)
    {
        if (FlyPrefab == null || InventoryTarget == null) return;

        GameObject go = Instantiate(FlyPrefab, transform);
        LootFlyEffect effect = go.GetComponent<LootFlyEffect>();

        // 转换终点坐标（从 UI 坐标转为屏幕/世界坐标）
        Vector3 endPos = InventoryTarget.position;

        effect.Play(icon, startWorldPos, endPos, () => {
            // 这里可以触发终点图标的微动反馈
            TriggerTargetPulse();
        });
    }
    public void SpawnScrapExplosion(Vector3 startPos, int scrapAmount)
    {
        if (ScrapPrefab == null || ScrapHUDTarget == null) return;

        // 根据获得的废料数量决定生成的颗粒数（最少3个，最多15个，防止卡顿）
        int visualCount = Mathf.Clamp(scrapAmount / 2, 3, 15);

        for (int i = 0; i < visualCount; i++)
        {
            GameObject go = Instantiate(ScrapPrefab, transform);
            ScrapFlyEffect effect = go.GetComponent<ScrapFlyEffect>();

            Sprite randomScrap = ScrapSprites[Random.Range(0, ScrapSprites.Length)];
            effect.Play(randomScrap, startPos, ScrapHUDTarget.position);
        }

        // 播放碎裂音效
        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Loot_ScrapIn);
    }
    private void TriggerTargetPulse()
    {
        // 播放入库音效
        if (GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ScrapIn);

        // 如果终点有动画组件，可以在这里触发
        // InventoryTarget.GetComponent<Animator>()?.SetTrigger("Pulse");
    }
}