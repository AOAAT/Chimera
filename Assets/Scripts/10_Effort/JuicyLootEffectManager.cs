using UnityEngine;

public class JuicyLootEffectManager : MonoBehaviour
{
    public static JuicyLootEffectManager Instance;

    [Header("=== 配置 ===")]
    public GameObject FlyPrefab; // 一个带有 Image 和 LootFlyEffect 脚本的预制体
    public RectTransform InventoryTarget; // 你手动设置的终点坐标物体

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

    private void TriggerTargetPulse()
    {
        // 播放入库音效
        if (GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(UISoundType.Loot_ScrapIn);

        // 如果终点有动画组件，可以在这里触发
        // InventoryTarget.GetComponent<Animator>()?.SetTrigger("Pulse");
    }
}