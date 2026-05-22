using UnityEngine;

public class HeadquartersBuilding : BuildingBase
{
    // 基地作为核心，可以在这里预留：
    // 1. 基地等级逻辑
    // 2. 基地血量/护盾逻辑
    // 3. 科技树解锁逻辑

    protected override void Awake()
    {
        base.Awake();
        // 基地通常是开局自带的，手动调用一次 OnPlaced 锁定网格
        // 如果是后来造的，则由 BuildingManager 调用
    }
}