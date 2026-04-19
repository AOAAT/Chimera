using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnGibs", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - Spawn Gibs (爆裂碎块)")]
public class Action_SpawnGibs : ECAAction
{
    [Header("=== 碎块池配置 ===")]
    public List<GameObject> GibPrefabs = new List<GameObject>();

    [Header("=== 爆炸参数 ===")]
    public int MinGibCount = 4;
    public int MaxGibCount = 8;

    [Header("=== 全屏血浆遮罩 (Screen Gore) ===")]
    public bool TriggerScreenBlood = true;
    public Color ScreenBloodColor = new Color(0.6f, 0f, 0f, 0.7f);

    public override void Execute(ECAContext context)
    {
        if (GibPrefabs == null || GibPrefabs.Count == 0 || context.ImpactPoint == null) return;

        int spawnCount = Random.Range(MinGibCount, MaxGibCount + 1);

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefabToSpawn = GibPrefabs[Random.Range(0, GibPrefabs.Count)];

            // 【核心修复 1】：不再使用 Instantiate，改用我们的 SimplePool 性能黑科技
            GameObject gibObj = SimplePool.Spawn(prefabToSpawn, context.ImpactPoint, Quaternion.identity);

            GibProjectile gibScript = gibObj.GetComponent<GibProjectile>();
            if (gibScript != null)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;

                // 【核心修复 2】：传入第二个参数 prefabToSpawn
                // 这样碎块在落地消失时，才知道该回哪个家！
                gibScript.Eject(randomDir, prefabToSpawn);
            }
        }

        if (TriggerScreenBlood && ScreenEffectManager.Instance != null)
        {
            ScreenEffectManager.Instance.TriggerFlash(ScreenBloodColor, 0.3f);
        }
    }
}