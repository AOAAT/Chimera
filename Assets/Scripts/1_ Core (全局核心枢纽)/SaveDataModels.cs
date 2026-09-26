using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    public const int CurrentVersion = 2;

    public int Version = CurrentVersion;
    public string SavedAtUtc;
    public string SceneName;
    public ResourceSaveData Resources = new ResourceSaveData();
    public List<ResidentSaveData> Residents = new List<ResidentSaveData>();
    public List<BuildingSaveData> Buildings = new List<BuildingSaveData>();
    public InventorySaveData Inventory = new InventorySaveData();
    public List<MechSaveData> DeployedMechs = new List<MechSaveData>();
}

[Serializable]
public class ResourceSaveData
{
    public float Scrap;
    public float Biomass;
    public float ManaStone;
}

[Serializable]
public class ResidentSaveData
{
    public string InstanceID;
    public string ResidentName;
    public bool IsHero;
    public int Level;
    public float Experience;
    public List<string> TraitIDs = new List<string>();
    public float TechProficiency;
    public float FleshProficiency;
    public float ManaProficiency;
    public float Discipline;
    public float Sociability;
    public float Courage;
    public ResidentStatus Status;
    public string CurrentCarrierID;
    public float CurrentHP;
    public bool HasWorldPosition;
    public SerializableVector3 WorldPosition;
}

[Serializable]
public class BuildingSaveData
{
    public string InstanceID;
    public string DefinitionID;
    public string RuntimeType;
    public SerializableVector3 WorldPosition;
    public List<string> StaffResidentIDs = new List<string>();
    public List<ProductionTaskSaveData> ProductionQueue = new List<ProductionTaskSaveData>();
    public float HeadquartersRecruitTimer;
    public SerializableVector3 RallyWorldPosition;
}

[Serializable]
public class ProductionTaskSaveData
{
    public string TaskID;
    public string DefinitionType;
    public string DefinitionID;
    public string ItemName;
    public float TotalTime;
    public float CurrentProgress;
    public bool IsPaused;
    public ResourceSet PaidCost;
}

[Serializable]
public class InventorySaveData
{
    public List<ComponentStackSaveData> ComponentWarehouse = new List<ComponentStackSaveData>();
    public List<ChassisStackSaveData> ChassisWarehouse = new List<ChassisStackSaveData>();
    public List<InstancedComponentSaveData> Components = new List<InstancedComponentSaveData>();
    public List<InstancedChassisSaveData> Chassis = new List<InstancedChassisSaveData>();
    public List<InstancedAccessorySaveData> Accessories = new List<InstancedAccessorySaveData>();
}

[Serializable]
public class ComponentStackSaveData
{
    public string DefinitionID;
    public int Level;
    public int Quantity;
}

[Serializable]
public class ChassisStackSaveData
{
    public string DefinitionID;
    public int Quantity;
}

[Serializable]
public class InstancedComponentSaveData
{
    public string InstanceID;
    public string DefinitionID;
    public string EquippedUnitID;
    public int CurrentMark;
    public List<string> SocketedAccessoryIDs = new List<string>();
}

[Serializable]
public class InstancedChassisSaveData
{
    public string InstanceID;
    public string DefinitionID;
    public string EquippedUnitID;
}

[Serializable]
public class InstancedAccessorySaveData
{
    public string InstanceID;
    public string DefinitionID;
    public string ParentComponentID;
}

[Serializable]
public class MechSaveData
{
    public string UnitID;
    public string UnitName;
    public string ChassisDefinitionID;
    public string ChassisInstanceID;
    public float CurrentHP;
    public float CurrentAP;
    public List<int> SlotIndices = new List<int>();
    public List<string> EquippedComponentIDs = new List<string>();
    public SerializableVector3 WorldPosition;
}

[Serializable]
public struct SerializableVector3
{
    public float X;
    public float Y;
    public float Z;

    public SerializableVector3(UnityEngine.Vector3 value)
    {
        X = value.x;
        Y = value.y;
        Z = value.z;
    }

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(X, Y, Z);
}
