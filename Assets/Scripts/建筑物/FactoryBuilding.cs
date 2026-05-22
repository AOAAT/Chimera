using UnityEngine;

public class FactoryBuilding : BuildingBase
{
    // 工厂目前不需要像组装厂那样处理 SpawnMech，
    // 它主要通过 UI 模块直接与仓库（InventoryManager）对话。

    protected override void Awake()
    {
        base.Awake();
        // 这里可以预留未来工厂等级、生产效率等逻辑
    }
}