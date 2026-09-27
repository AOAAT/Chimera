using UnityEngine;

[CreateAssetMenu(fileName = "Act_SpawnEnemy", menuName = "Chimera Protocol/2. ECA 机制积木/战术 - 召唤敌人(适配度量衡)")]
public class Action_SpawnEnemy : ECAAction
{
    [Header("=== 召唤配置 ===")]
    public EnemyDataSO EnemyToSpawn;
    public int SpawnCount = 1;

    [Tooltip("召唤半径。将自动适配全局度量衡。")]
    public float SpawnRadius = 2f;

    [Header("=== 预制体引用 ===")]
    [Tooltip("通常填入项目通用的 BaseEnemyPrefab")]
    public GameObject EnemyBasePrefab;

    public override void Execute(ECAContext context)
    {
        if (EnemyToSpawn == null || EnemyBasePrefab == null || context.SourceEntity == null) return;

        // --- 👇【核心修改】：读取全局度量衡 ---
        float realRadius = CombatSandbox.GetDist(SpawnRadius);

        for (int i = 0; i < SpawnCount; i++)
        {
            // 1. 在缩放后的半径内计算随机出生点
            Vector2 randomOffset = Random.insideUnitCircle * realRadius;
            Vector3 spawnPos = context.SourceEntity.position + new Vector3(randomOffset.x, randomOffset.y, 0);

            // 2. 实例化
            GameObject newEnemy = Instantiate(EnemyBasePrefab, spawnPos, Quaternion.identity);
            newEnemy.name = $"[Summoned] {EnemyToSpawn.EnemyName}";

            // 3. 注入数据
            EnemyBrain brain = newEnemy.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.MyData = EnemyToSpawn;
                // 此时 EnemyBrain 内部会处理自己的 VisualScaleMultiplier
            }

            // 4. 调试反馈
            Debug.Log($"<color=#FF00FF>【空间增殖】</color> {context.SourceEntity.name} 在半径 {realRadius:F1} 内召唤了 {EnemyToSpawn.EnemyName}");
        }
    }
}