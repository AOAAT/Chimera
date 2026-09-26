using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum StorageKind { Warehouse, OrderInput, FactoryOutput, Backpack, Recovery }

[Serializable]
public class CargoStack
{
    public string Key;
    public float Amount;
    public CargoStack(string key, float amount) { Key = key; Amount = amount; }
}

[Serializable]
public class LogisticsStorage
{
    public string ID;
    public string OwnerID;
    public string Name;
    public StorageKind Kind;
    public SerializableVector3 Position;
    public float Capacity = 1000;
    public bool AcceptResources = true;
    public bool AcceptItems = true;
    public List<CargoStack> Cargo = new List<CargoStack>();
    public float Used => Cargo.Sum(x => x.Amount);
    public float Count(string key) => Cargo.Where(x => x.Key == key).Sum(x => x.Amount);
    public bool Accepts(string key) => LogisticsKeys.IsResource(key) ? AcceptResources : AcceptItems;
    public void Add(string key, float amount)
    {
        if (amount <= 0) return;
        var stack = Cargo.Find(x => x.Key == key);
        if (stack == null) Cargo.Add(new CargoStack(key, amount));
        else stack.Amount += amount;
    }
    public bool Remove(string key, float amount)
    {
        if (amount <= 0 || Count(key) + 0.0001f < amount) return false;
        foreach (var stack in Cargo.Where(x => x.Key == key).ToArray())
        {
            float take = Mathf.Min(stack.Amount, amount);
            stack.Amount -= take; amount -= take;
            if (stack.Amount < 0.0001f) Cargo.Remove(stack);
        }
        return true;
    }
}

[Serializable]
public class HaulJob
{
    public string ID = Guid.NewGuid().ToString();
    public string SourceID;
    public string TargetID;
    public string Key;
    public float Amount;
    public string WorkerID;
    public string OrderID;
    public bool PickedUp;
    [NonSerialized] public bool Moving;
    [NonSerialized] public Vector3 LastPosition;
    [NonSerialized] public float Stalled;
    [NonSerialized] public float RetryAt;
}

[Serializable]
public class LogisticsSaveData
{
    public List<LogisticsStorage> Storages = new List<LogisticsStorage>();
    public List<HaulJob> Jobs = new List<HaulJob>();
}

public static class LogisticsKeys
{
    public const string Scrap = "resource:scrap", Biomass = "resource:biomass", Mana = "resource:mana";
    public static bool IsResource(string key) => key != null && key.StartsWith("resource:");
    public static IEnumerable<CargoStack> Resources(ResourceSet set)
    {
        if (set.Scrap > 0) yield return new CargoStack(Scrap, set.Scrap);
        if (set.Biomass > 0) yield return new CargoStack(Biomass, set.Biomass);
        if (set.ManaStone > 0) yield return new CargoStack(Mana, set.ManaStone);
    }
    public static bool Valid(ResourceSet set) => ValidNumber(set.Scrap) && ValidNumber(set.Biomass) && ValidNumber(set.ManaStone);
    private static bool ValidNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    public static string Name(string key)
    {
        if (key == Scrap) return "废料";
        if (key == Biomass) return "生物质";
        if (key == Mana) return "魔晶";
        var inventory = PlayerInventoryManager.Instance;
        if (key != null && key.StartsWith("component:"))
            return inventory?.GetComponentInstance(key.Substring(10))?.BaseData?.ComponentName ?? "组件";
        if (key != null && key.StartsWith("chassis:"))
            return inventory?.AllChassisDatabase.Find(x => x != null && x.ChassisID == key.Substring(8))?.ChassisName ?? "底盘";
        return key ?? "货物";
    }
}
