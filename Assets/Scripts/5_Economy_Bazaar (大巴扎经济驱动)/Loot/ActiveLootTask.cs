// --- ActiveLootTask.cs ---
using System.Collections.Generic;

public class ActiveLootTask
{
    public LootTaskConfig Config;
    public bool IsClaimed = false;
    public bool IsBoxOpened = false;
    public bool IsForceClaim = false;

    public SubTag? LockedTag = null;

    // 原有的组件列表
    public List<InstancedComponent> GeneratedItems = new List<InstancedComponent>();

    // 👇【核心新增】：底盘列表
    public List<InstancedChassis> GeneratedChassis = new List<InstancedChassis>();

    // 判定这个任务是否有底盘奖励
    public bool HasChassis => GeneratedChassis != null && GeneratedChassis.Count > 0;
}