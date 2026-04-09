using System.Collections.Generic;

// 运行时的“盲盒状态追踪器”
public class ActiveLootTask
{
    public LootTaskConfig Config; // 原始配置表数据

    // === 状态机标志 ===
    public bool IsClaimed = false;   // 是否已经被拿走/粉碎？(打勾变灰)
    public bool IsBoxOpened = false; // 盲盒是否已经被打开了？(锁定防作弊)

    // === 锁死的数据缓存 (防悔棋作弊的核心) ===
    public SubTag? LockedTag = null; // 玩家在深度查找中选定的标签
    public List<InstancedComponent> GeneratedItems = new List<InstancedComponent>(); // 抽出来的具体装备
}