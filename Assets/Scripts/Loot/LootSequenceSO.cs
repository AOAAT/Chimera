using System;
using System.Collections.Generic;
using UnityEngine;

public enum LootDropMode
{
    MacroCategorySingle, // 模式1：大类保底单抽
    SystemAssignedTag,   // 模式2：系统指定盲盒
    PlayerDrivenFilter,  // 模式3：定向双重筛选 (玩家三选一Tag)
    CustomPoolDrop       // 模式4：策划纯手动配置池
}

[Serializable]
public class CustomDropEntry
{
    public ComponentDataSO Blueprint; // 具体掉哪个图纸
    [Range(1, 4)] public int Level = 1; // 具体掉几级！
}

[Serializable]
public class LootTaskConfig
{
    [Header("掉落模式")]
    public LootDropMode Mode;

    [Header("多态生成：三选一概率")]
    [Range(0f, 1f)] public float TripleChoiceProbability = 0f;

    [Header("模式4专属：自定义奖池 (Custom Pool)")]
    [Tooltip("仅当 Mode 为 CustomPoolDrop 时生效。系统会从这里面随机抽 1 个或 3 个。")]
    public List<CustomDropEntry> CustomPool = new List<CustomDropEntry>();
}

// 👇【核心】：这个类名必须和文件名 LootSequenceSO.cs 一模一样！
[CreateAssetMenu(fileName = "NewLootSequence", menuName = "Chimera Protocol/Economy/Loot Sequence Config")]
public class LootSequenceSO : ScriptableObject
{
    public List<LootTaskConfig> Tasks = new List<LootTaskConfig>();
}