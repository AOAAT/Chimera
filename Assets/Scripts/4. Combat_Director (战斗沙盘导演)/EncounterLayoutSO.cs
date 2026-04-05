using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemySpawnData
{
    public EnemyDataSO EnemyType;
    public Vector2 LocalPosition; // 相对于战场中心的相对坐标
}

[CreateAssetMenu(fileName = "NewEncounterLayout", menuName = "Chimera Protocol/3. 宏观控制/战斗房间布局 (Encounter Layout)")]
public class EncounterLayoutSO : ScriptableObject
{
    [Header("=== 战场视觉参考 (沙盘 2.0 核心) ===")]
    [Tooltip("将您的【战斗场地预制体】拖入此处！沙盘会自动读取它的真实贴图和物理边界！")]
    public GameObject ArenaReference;

    [Header("=== 阵型配置 ===")]
    public List<EnemySpawnData> Enemies = new List<EnemySpawnData>();

    [Header("=== 玩家禁飞区 (楚河汉界) ===")]
    [Tooltip("相对于场地中心的绝对物理坐标 (X,Y 为区域左上角)")]
    public List<Rect> ForbiddenZones = new List<Rect>();

    // 👇【终极接管】：这就是你刚刚配好的那张带有“模式4(自定义)”或“模式3(三选一)”的大巴扎掉落表！
    [Header("=== 战利品掉落序列 (The Bazaar Loot) ===")]
    public LootSequenceSO NodeLootSequence;

}