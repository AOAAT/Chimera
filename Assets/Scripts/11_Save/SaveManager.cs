using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    private string SavePath => Path.Combine(Application.persistentDataPath, "ChimeraSave_01.json");

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // ==========================================
    // 💾 保存逻辑：抓取 -> 拍扁 -> 写入
    // ==========================================
    public void SaveGame()
    {
        SaveData data = new SaveData();

        // 1. 抓取资源
        var res = GlobalResourceManager.Instance;
        data.Materials = res.Materials;
        data.CurrentSAN = res.CurrentSAN;
        data.MaxSAN = res.MaxSAN;
        data.MaxPowerCapacity = res.MaxPowerCapacity;
        data.DaysSurvived = res.DaysSurvived;

        // 2. 抓取地图 (MapManager)
        data.CurrentNodeID = MapManager.Instance.CurrentNodeID;
        data.CurrentLayer = MapManager.Instance.CurrentLayer;
        // 获取 MapGenerator 里的字典
        var nodes = MapManager.Instance.GetComponent<MapGenerator>().GeneratedMap;
        foreach (var node in nodes.Values)
        {
            data.MapNodes.Add(new MapNodeSaveEntry { NodeID = node.NodeID, State = node.NodeState, IsRevealed = node.IsRevealed });
        }

        // 3. 抓取仓库
        var inv = PlayerInventoryManager.Instance;
        foreach (var c in inv.ChassisInventory)
            data.ChassisInventory.Add(new ChassisSaveEntry { InstanceID = c.InstanceID, BlueprintID = c.BaseData.ChassisID, EquippedUnitID = c.EquippedUnitID });

        foreach (var cp in inv.ComponentInventory)
            data.ComponentInventory.Add(new ComponentSaveEntry { InstanceID = cp.InstanceID, BlueprintID = cp.BaseData.ComponentBaseID, EquippedUnitID = cp.EquippedUnitID, CurrentLevel = cp.CurrentLevel });

        // 4. 抓取机库
        for (int i = 0; i < 8; i++)
        {
            var unit = inv.HangarUnits[i];
            if (unit == null) continue;
            data.HangarUnits[i] = new UnitProfileSaveEntry
            {
                UnitID = unit.UnitID,
                UnitName = unit.UnitName,
                ChassisDataID = unit.ChassisData.ChassisID,
                CurrentHP = unit.CurrentHP,
                CurrentAP = unit.CurrentAP,
                IsDeployed = false, // 存档时不处于部署态
                SlotIndices = unit.SlotIndices,
                EquippedComponentIDs = unit.EquippedComponentIDs
            };
        }

        // 序列化并写入
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"<color=green>【存档成功】</color> 路径: {SavePath}");
    }

    // ==========================================
    // 📂 读取逻辑：读取 -> 还原 -> 分发
    // ==========================================
    public bool LoadGame()
    {
        if (!File.Exists(SavePath)) return false;

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        // 1. 还原资源
        var res = GlobalResourceManager.Instance;
        res.Materials = data.Materials;
        res.CurrentSAN = data.CurrentSAN;
        res.MaxSAN = data.MaxSAN;
        res.MaxPowerCapacity = data.MaxPowerCapacity;
        res.DaysSurvived = data.DaysSurvived;

        // 2. 还原仓库 (核心：基于 ID 找 SO)
        var inv = PlayerInventoryManager.Instance;
        inv.ChassisInventory.Clear();
        inv.ComponentInventory.Clear();

        foreach (var cEntry in data.ChassisInventory)
        {
            var so = inv.AllChassisDatabase.Find(x => x.ChassisID == cEntry.BlueprintID);
            if (so != null)
            {
                var inst = new InstancedChassis(so) { InstanceID = cEntry.InstanceID, EquippedUnitID = cEntry.EquippedUnitID };
                inv.ChassisInventory.Add(inst);
            }
        }

        foreach (var cpEntry in data.ComponentInventory)
        {
            var so = inv.AllComponentDatabase.Find(x => x.ComponentBaseID == cpEntry.BlueprintID);
            if (so != null)
            {
                var inst = new InstancedComponent(so, cpEntry.CurrentLevel) { InstanceID = cpEntry.InstanceID, EquippedUnitID = cpEntry.EquippedUnitID };
                inv.ComponentInventory.Add(inst);
            }
        }

        // 3. 还原机库
        inv.HangarUnits = new SavedUnitProfile[8];
        for (int i = 0; i < 8; i++)
        {
            var uEntry = data.HangarUnits[i];
            if (uEntry == null || string.IsNullOrEmpty(uEntry.ChassisDataID)) continue;

            var chassisSO = inv.AllChassisDatabase.Find(x => x.ChassisID == uEntry.ChassisDataID);
            if (chassisSO != null)
            {
                var dummyInst = new InstancedChassis(chassisSO) { InstanceID = "TEMP" }; // 构造函数需要
                var profile = new SavedUnitProfile(dummyInst, uEntry.UnitName)
                {
                    UnitID = uEntry.UnitID,
                    CurrentHP = uEntry.CurrentHP,
                    CurrentAP = uEntry.CurrentAP,
                    IsDeployed = false,
                    SlotIndices = uEntry.SlotIndices,
                    EquippedComponentIDs = uEntry.EquippedComponentIDs
                };
                inv.HangarUnits[i] = profile;
            }
        }

        // 4. 还原地图
        MapManager.Instance.CurrentNodeID = data.CurrentNodeID;
        MapManager.Instance.CurrentLayer = data.CurrentLayer;
        var mapNodes = MapManager.Instance.GetComponent<MapGenerator>().GeneratedMap;
        foreach (var nodeSave in data.MapNodes)
        {
            if (mapNodes.ContainsKey(nodeSave.NodeID))
            {
                mapNodes[nodeSave.NodeID].NodeState = nodeSave.State;
                mapNodes[nodeSave.NodeID].IsRevealed = nodeSave.IsRevealed;
            }
        }

        inv.ForceTriggerInventoryEvent();
        Debug.Log("<color=cyan>【读档成功】</color> 已还原所有机甲与资源。");
        return true;
    }

    public bool HasSaveFile() => File.Exists(SavePath);
}