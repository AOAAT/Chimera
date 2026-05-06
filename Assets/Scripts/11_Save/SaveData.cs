using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // 1. 全局资源
    public int Materials;
    public int CurrentSAN;
    public int MaxSAN;
    public int MaxPowerCapacity;
    public int DaysSurvived;

    // 2. 地图进度
    public string CurrentNodeID;
    public int CurrentLayer;
    public List<MapNodeSaveEntry> MapNodes = new List<MapNodeSaveEntry>();

    // 3. 实体仓库
    public List<ChassisSaveEntry> ChassisInventory = new List<ChassisSaveEntry>();
    public List<ComponentSaveEntry> ComponentInventory = new List<ComponentSaveEntry>();

    // 4. 机库位置 (8个车位)
    public UnitProfileSaveEntry[] HangarUnits = new UnitProfileSaveEntry[8];
}

[Serializable]
public class MapNodeSaveEntry
{
    public string NodeID;
    public MapNodeState State;
    public bool IsRevealed;
}

[Serializable]
public class ChassisSaveEntry
{
    public string InstanceID;
    public string BlueprintID; // ChassisDataSO.ChassisID
    public string EquippedUnitID;
}

[Serializable]
public class ComponentSaveEntry
{
    public string InstanceID;
    public string BlueprintID; // ComponentDataSO.ComponentBaseID
    public string EquippedUnitID;
    public int CurrentLevel;
}

[Serializable]
public class UnitProfileSaveEntry
{
    public string UnitID;
    public string UnitName;
    public string ChassisDataID; // ChassisDataSO.ChassisID
    public float CurrentHP;
    public float CurrentAP;
    public bool IsDeployed;
    public List<int> SlotIndices;
    public List<string> EquippedComponentIDs;
}