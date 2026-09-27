using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Converts persistent string IDs to authored ScriptableObject definitions.
/// Save files never rely on Unity object references or asset GUIDs.
/// </summary>
public sealed class SaveDefinitionResolver
{
    private readonly Dictionary<string, ChassisDataSO> chassis = new Dictionary<string, ChassisDataSO>();
    private readonly Dictionary<string, ComponentDataSO> components = new Dictionary<string, ComponentDataSO>();
    private readonly Dictionary<string, AccessoryDataSO> accessories = new Dictionary<string, AccessoryDataSO>();
    private readonly Dictionary<string, BuildingDataSO> buildings = new Dictionary<string, BuildingDataSO>();

    public SaveDefinitionResolver(PlayerInventoryManager inventory, BuildingManager buildingManager)
    {
        if (inventory != null)
        {
            AddRange(inventory.AllChassisDatabase, x => x.ChassisID, chassis, "底盘");
            AddRange(inventory.DebugChassisBundle, x => x.ChassisID, chassis, "底盘");
            AddRange(inventory.AllComponentDatabase, x => x.ComponentBaseID, components, "组件");
            AddRange(inventory.DebugComponentBundle, x => x.ComponentBaseID, components, "组件");
            AddRange(inventory.AllAccessoryDatabase, x => x.AccessoryID, accessories, "芯片");
            AddRange(inventory.DebugAccessoryBundle, x => x.AccessoryID, accessories, "芯片");
        }

        if (buildingManager != null)
            AddRange(buildingManager.BuildingDatabase, x => x.BuildingID, buildings, "建筑");
    }

    public ChassisDataSO ResolveChassis(string id) => Resolve(id, chassis, "底盘");
    public ComponentDataSO ResolveComponent(string id) => Resolve(id, components, "组件");
    public AccessoryDataSO ResolveAccessory(string id) => Resolve(id, accessories, "芯片");
    public BuildingDataSO ResolveBuilding(string id) => Resolve(id, buildings, "建筑");

    private static T Resolve<T>(string id, Dictionary<string, T> map, string label) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (map.TryGetValue(id, out T result)) return result;
        Debug.LogWarning($"[存档] 找不到{label}定义 ID: {id}");
        return null;
    }

    private static void AddRange<T>(IEnumerable<T> source, Func<T, string> getID, Dictionary<string, T> target, string label)
        where T : UnityEngine.Object
    {
        if (source == null) return;
        foreach (T entry in source)
        {
            if (entry == null) continue;
            string id = getID(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"[存档] {label}定义 {entry.name} 缺少稳定 ID，无法写入存档。");
                continue;
            }

            if (target.TryGetValue(id, out T existing) && existing != entry)
            {
                Debug.LogError($"[存档] {label}稳定 ID 重复: {id} ({existing.name}, {entry.name})");
                continue;
            }
            target[id] = entry;
        }
    }
}
