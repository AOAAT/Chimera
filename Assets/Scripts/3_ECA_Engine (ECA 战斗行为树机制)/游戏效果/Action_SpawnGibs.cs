// --- START OF FILE Action_SpawnGibs.cs ---
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnGibs", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - Spawn Gibs (爆裂碎块)")]
public class Action_SpawnGibs : ECAAction
{
    [Header("=== 碎块池配置 ===")]
    [Tooltip("把做好的碎块预制体(挂了GibProjectile)全拖进来！")]
    public List<GameObject> GibPrefabs = new List<GameObject>();

    [Header("=== 爆炸参数 ===")]
    [Tooltip("每次爆炸随机喷出多少个碎块？")]
    public int MinGibCount = 4;
    public int MaxGibCount = 8;

    [Header("=== 全屏血浆遮罩 (Screen Gore) ===")]
    [Tooltip("如果勾选，死的时候屏幕上会溅上一层渐隐的血迹 UI！")]
    public bool TriggerScreenBlood = true;
    public Color ScreenBloodColor = new Color(0.6f, 0f, 0f, 0.7f); // 暗红色

    public override void Execute(ECAContext context)
    {
        if (GibPrefabs == null || GibPrefabs.Count == 0 || context.ImpactPoint == null) return;

        // 1. 计算爆裂数量
        int spawnCount = Random.Range(MinGibCount, MaxGibCount + 1);

        // 2. 确定喷射的基准方向 (向四周炸开)
        for (int i = 0; i < spawnCount; i++)
        {
            // 从碎块池里随机抽一个
            GameObject prefabToSpawn = GibPrefabs[Random.Range(0, GibPrefabs.Count)];

            // 生成在死亡位置
            GameObject gibObj = Instantiate(prefabToSpawn, context.ImpactPoint, Quaternion.identity);

            GibProjectile gibScript = gibObj.GetComponent<GibProjectile>();
            if (gibScript != null)
            {
                // 给它一个完全随机的 360 度发射方向
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                gibScript.Eject(randomDir);
            }
        }

        // 3. 呼叫全屏血浆闪烁 (复用我们之前写的 ScreenEffectManager！)
        if (TriggerScreenBlood && ScreenEffectManager.Instance != null)
        {
            // 闪烁 1.5 秒的红色血污
            ScreenEffectManager.Instance.TriggerFlash(ScreenBloodColor, 0.3f);
        }
    }
}