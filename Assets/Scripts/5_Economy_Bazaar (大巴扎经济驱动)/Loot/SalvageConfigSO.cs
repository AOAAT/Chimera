using UnityEngine;

[CreateAssetMenu(fileName = "SalvageConfig", menuName = "Chimera Protocol/Economy/Salvage Config")]
public class SalvageConfigSO : ScriptableObject
{
    [Header("=== 第二阶段：多态生成概率 ===")]
    [Range(0f, 1f)] public float SingleDropChance = 0.6f; // 60%概率出单件
    [Range(0f, 1f)] public float DraftThreeChance = 0.4f; // 40%概率出三选一
    public int GetWeightForLevel(int currentMapDepth, int targetLevel)
    {
        // 这里是你未来操控玩家成长曲线的绝对核心枢纽！
        // 示例算法：越深的地方，1级权重越低，2/3级权重越高
        if (targetLevel == 1) return Mathf.Max(0, 100 - (currentMapDepth * 10)); // 深度越深，1级越少
        if (targetLevel == 2) return currentMapDepth >= 3 ? 30 + (currentMapDepth * 5) : 0; // 3层开始掉2级
        if (targetLevel == 3) return currentMapDepth >= 6 ? 10 + (currentMapDepth * 5) : 0; // 6层开始掉3级
        return 0; // 4 级绝对不掉落，只能通过车间电焊合成！
    }
}