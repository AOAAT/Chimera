using System.Collections.Generic;

public class ActiveLootTask
{
    public LootTaskConfig Config;
    public bool IsClaimed = false;
    public bool IsBoxOpened = false;

    // --- 👇【核心新增】：强制领取标志 ---
    public bool IsForceClaim = false;

    public SubTag? LockedTag = null;
    public List<InstancedComponent> GeneratedItems = new List<InstancedComponent>();
}