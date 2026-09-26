using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the save transaction. Runtime objects are converted to plain ID-based data,
/// written as JSON, then rebuilt in a deterministic order after a scene reload.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }
    public const string DefaultFileName = "chimera_save_0.json";

    public bool IsLoading { get; private set; }
    public string DefaultSavePath => Path.Combine(Application.persistentDataPath, DefaultFileName);
    public event Action OnSaveCompleted;
    public event Action OnLoadCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject host = new GameObject(nameof(SaveGameManager));
        host.AddComponent<SaveGameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) SaveDefault();
        if (Input.GetKeyDown(KeyCode.F9)) LoadDefault();
    }
#endif

    public bool HasDefaultSave() => File.Exists(DefaultSavePath);

    public bool SaveDefault()
    {
        try
        {
            GameSaveData data = CaptureCurrentGame();
            string json = JsonUtility.ToJson(data, true);
            WriteAtomically(DefaultSavePath, json);
            Debug.Log($"<color=green>【存档】</color> 已保存到 {DefaultSavePath}");
            OnSaveCompleted?.Invoke();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[存档] 保存失败，旧存档未被覆盖。\n{exception}");
            return false;
        }
    }

    public bool LoadDefault()
    {
        if (IsLoading) return false;
        if (!File.Exists(DefaultSavePath))
        {
            Debug.LogWarning($"[存档] 未找到存档: {DefaultSavePath}");
            return false;
        }

        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(DefaultSavePath));
            ValidateAndNormalize(data);
            StartCoroutine(LoadRoutine(data));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[存档] 读取失败。\n{exception}");
            return false;
        }
    }

    private GameSaveData CaptureCurrentGame()
    {
        if (GlobalResourceManager.Instance == null || PopulationManager.Instance == null ||
            PlayerInventoryManager.Instance == null || BuildingManager.Instance == null)
            throw new InvalidOperationException("当前场景不是可存档的殖民地场景，核心管理器尚未就绪。");

        GameSaveData save = new GameSaveData
        {
            SavedAtUtc = DateTime.UtcNow.ToString("O"),
            SceneName = SceneManager.GetActiveScene().name,
            Resources = new ResourceSaveData
            {
                Scrap = GlobalResourceManager.Instance.CurrentScrap,
                Biomass = GlobalResourceManager.Instance.CurrentBiomass,
                ManaStone = GlobalResourceManager.Instance.CurrentManaStone
            },
            Inventory = PlayerInventoryManager.Instance.CaptureSaveData()
        };

        Dictionary<string, ResidentEntity> residentEntities = FindObjectsOfType<ResidentEntity>()
            .Where(x => x.MyData != null && !string.IsNullOrWhiteSpace(x.MyData.InstanceID))
            .GroupBy(x => x.MyData.InstanceID)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (ResidentData resident in PopulationManager.Instance.TotalResidents)
        {
            if (resident == null) continue;
            ResidentSaveData item = CaptureResident(resident);
            if (residentEntities.TryGetValue(resident.InstanceID, out ResidentEntity entity))
            {
                entity.SyncRuntimeStateToData();
                item.CurrentHP = resident.CurrentHP;
                item.HasWorldPosition = true;
                item.WorldPosition = new SerializableVector3(entity.transform.position);
            }
            save.Residents.Add(item);
        }

        foreach (BuildingBase building in BuildingBase.AllPlacedBuildings.Where(x => x != null))
        {
            BuildingSaveData item = new BuildingSaveData
            {
                InstanceID = building.PersistentID,
                DefinitionID = building.DefinitionID,
                RuntimeType = building.GetType().Name,
                WorldPosition = new SerializableVector3(building.transform.position),
                StaffResidentIDs = building.GetStaffList()
                    .Where(x => x != null).Select(x => x.InstanceID).ToList()
            };
            if (building is FactoryBuilding factory)
                item.ProductionQueue = factory.CaptureProductionQueue();
            if (building is HeadquartersBuilding headquarters)
                item.HeadquartersRecruitTimer = headquarters.RecruitTimer;
            if (building is AssemblerBuilding assembler)
                item.RallyWorldPosition = new SerializableVector3(assembler.RallyWorldPos);
            save.Buildings.Add(item);
        }

        foreach (MechUnit2D mech in FindObjectsOfType<MechUnit2D>())
        {
            DamageReceiver receiver = mech.GetComponent<DamageReceiver>();
            SavedUnitProfile profile = mech.GetProfile();
            if (profile == null || profile.ChassisData == null || (receiver != null && receiver.isEnemy)) continue;
            if (receiver != null)
            {
                profile.CurrentHP = receiver.CurrentHP;
                profile.CurrentAP = receiver.CurrentAP;
            }
            save.DeployedMechs.Add(new MechSaveData
            {
                UnitID = profile.UnitID,
                UnitName = profile.UnitName,
                ChassisDefinitionID = profile.ChassisData.ChassisID,
                ChassisInstanceID = profile.ChassisInstanceID,
                CurrentHP = profile.CurrentHP,
                CurrentAP = profile.CurrentAP,
                SlotIndices = new List<int>(profile.SlotIndices),
                EquippedComponentIDs = new List<string>(profile.EquippedComponentIDs),
                WorldPosition = new SerializableVector3(mech.transform.position)
            });
        }
        return save;
    }

    private IEnumerator LoadRoutine(GameSaveData save)
    {
        IsLoading = true;
        Time.timeScale = 1f;
        AsyncOperation operation = SceneManager.LoadSceneAsync(save.SceneName);
        if (operation == null)
        {
            IsLoading = false;
            yield break;
        }
        yield return operation;
        yield return null;

        try
        {
            ApplyLoadedGame(save);
            Debug.Log($"<color=green>【存档】</color> 已恢复 {save.SavedAtUtc} 的殖民地快照。");
            OnLoadCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[存档] 场景已载入，但恢复数据失败。\n{exception}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyLoadedGame(GameSaveData save)
    {
        if (GlobalResourceManager.Instance == null || PopulationManager.Instance == null ||
            PlayerInventoryManager.Instance == null || BuildingManager.Instance == null)
            throw new InvalidOperationException("目标场景缺少恢复存档所需的核心管理器。");

        SaveDefinitionResolver definitions = new SaveDefinitionResolver(
            PlayerInventoryManager.Instance, BuildingManager.Instance);
        GlobalResourceManager.Instance.RestoreResources(save.Resources);
        PlayerInventoryManager.Instance.RestoreSaveData(save.Inventory, definitions);

        Dictionary<string, ResidentData> residents = new Dictionary<string, ResidentData>();
        foreach (ResidentSaveData item in save.Residents)
        {
            ResidentData resident = RestoreResident(item);
            residents[resident.InstanceID] = resident;
        }
        PopulationManager.Instance.ReplaceResidents(residents.Values.ToList());

        Dictionary<string, BuildingBase> existing = BuildingBase.AllPlacedBuildings
            .Where(x => x != null).GroupBy(x => x.PersistentID).ToDictionary(x => x.Key, x => x.First());
        HashSet<string> restoredBuildingIDs = new HashSet<string>();
        HashSet<string> assignedResidentIDs = new HashSet<string>();
        foreach (BuildingSaveData item in save.Buildings)
        {
            BuildingBase building = null;
            existing.TryGetValue(item.InstanceID, out building);
            if (building == null && !string.IsNullOrWhiteSpace(item.DefinitionID))
            {
                BuildingDataSO definition = definitions.ResolveBuilding(item.DefinitionID);
                if (definition != null && definition.Prefab != null)
                {
                    GameObject go = Instantiate(definition.Prefab, item.WorldPosition.ToVector3(), Quaternion.identity);
                    building = go.GetComponent<BuildingBase>();
                    if (building != null) building.InitializePersistence(item.DefinitionID);
                }
            }
            if (building == null)
            {
                Debug.LogWarning($"[存档] 无法恢复建筑 {item.InstanceID} ({item.RuntimeType})。");
                continue;
            }

            building.RestorePersistence(item.InstanceID, item.DefinitionID, item.WorldPosition.ToVector3());
            building.RestoreStaff(item.StaffResidentIDs.Where(residents.ContainsKey).Select(id => residents[id]));
            foreach (ResidentData staff in building.GetStaffList())
                assignedResidentIDs.Add(staff.InstanceID);
            if (building is FactoryBuilding factory)
                factory.RestoreProductionQueue(item.ProductionQueue, definitions);
            if (building is HeadquartersBuilding headquarters)
                headquarters.RestoreRecruitTimer(item.HeadquartersRecruitTimer);
            if (building is AssemblerBuilding assembler)
                assembler.RallyWorldPos = item.RallyWorldPosition.ToVector3();
            restoredBuildingIDs.Add(item.InstanceID);
        }

        foreach (BuildingBase building in BuildingBase.AllPlacedBuildings.ToArray())
        {
            if (building != null && !restoredBuildingIDs.Contains(building.PersistentID))
                Destroy(building.gameObject);
        }

        Dictionary<string, string> pendingStaffTargets = new Dictionary<string, string>();
        foreach (ResidentSaveData item in save.Residents)
        {
            if (!residents.TryGetValue(item.InstanceID, out ResidentData resident)) continue;
            if (assignedResidentIDs.Contains(resident.InstanceID)) continue;
            if (item.Status == ResidentStatus.TravelingToWork && !string.IsNullOrWhiteSpace(item.CurrentCarrierID))
                pendingStaffTargets[resident.InstanceID] = item.CurrentCarrierID;
            resident.Status = ResidentStatus.Idle;
            resident.CurrentCarrierID = string.Empty;
        }

        foreach (ResidentSaveData item in save.Residents)
        {
            if (!residents.TryGetValue(item.InstanceID, out ResidentData resident)) continue;
            if (assignedResidentIDs.Contains(resident.InstanceID)) continue;
            Vector3 position = item.HasWorldPosition ? item.WorldPosition.ToVector3() : Vector3.zero;
            ResidentEntity entity = PopulationManager.Instance.SpawnExistingResidentAt(resident, position);
            if (entity == null || !pendingStaffTargets.TryGetValue(resident.InstanceID, out string targetID))
                continue;

            BuildingBase target = BuildingBase.AllPlacedBuildings.FirstOrDefault(building =>
                building != null && building.PersistentID == targetID);
            if (target != null)
                entity.OrderGarrison(target);
        }
        PopulationManager.Instance.RefreshMaxCapacity();

        AssemblerBuilding mechSpawner = FindObjectOfType<AssemblerBuilding>();
        if (mechSpawner == null && save.DeployedMechs.Count > 0)
            Debug.LogWarning("[存档] 场景中没有装配建筑，已部署机甲无法恢复。");
        else if (mechSpawner != null)
        {
            foreach (MechSaveData item in save.DeployedMechs)
            {
                ChassisDataSO chassis = definitions.ResolveChassis(item.ChassisDefinitionID);
                if (chassis == null) continue;
                SavedUnitProfile profile = new SavedUnitProfile(new InstancedChassis(chassis), item.UnitName)
                {
                    UnitID = item.UnitID,
                    ChassisInstanceID = item.ChassisInstanceID,
                    CurrentHP = item.CurrentHP,
                    CurrentAP = item.CurrentAP,
                    IsDeployed = true,
                    SlotIndices = new List<int>(item.SlotIndices),
                    EquippedComponentIDs = new List<string>(item.EquippedComponentIDs)
                };
                mechSpawner.SpawnMechAt(profile, item.WorldPosition.ToVector3(), false);
            }
        }
    }

    private static ResidentSaveData CaptureResident(ResidentData resident)
    {
        return new ResidentSaveData
        {
            InstanceID = resident.InstanceID,
            ResidentName = resident.ResidentName,
            IsHero = resident.IsHero,
            Level = resident.Level,
            Experience = resident.Experience,
            TraitIDs = new List<string>(resident.TraitIDs ?? new List<string>()),
            TechProficiency = resident.TechProficiency,
            FleshProficiency = resident.FleshProficiency,
            ManaProficiency = resident.ManaProficiency,
            Discipline = resident.Discipline,
            Sociability = resident.Sociability,
            Courage = resident.Courage,
            Status = resident.Status,
            CurrentCarrierID = resident.CurrentCarrierID,
            CurrentHP = resident.CurrentHP
        };
    }

    private static ResidentData RestoreResident(ResidentSaveData item)
    {
        return new ResidentData(item.ResidentName, item.IsHero)
        {
            InstanceID = item.InstanceID,
            Level = item.Level,
            Experience = item.Experience,
            TraitIDs = new List<string>(item.TraitIDs ?? new List<string>()),
            TechProficiency = item.TechProficiency,
            FleshProficiency = item.FleshProficiency,
            ManaProficiency = item.ManaProficiency,
            Discipline = item.Discipline,
            Sociability = item.Sociability,
            Courage = item.Courage,
            Status = item.Status,
            CurrentCarrierID = item.CurrentCarrierID,
            CurrentHP = item.CurrentHP
        };
    }

    private static void ValidateAndNormalize(GameSaveData save)
    {
        if (save == null) throw new InvalidDataException("存档内容为空。");
        if (save.Version <= 0 || save.Version > GameSaveData.CurrentVersion)
            throw new InvalidDataException($"不支持的存档版本: {save.Version}");
        if (string.IsNullOrWhiteSpace(save.SceneName))
            throw new InvalidDataException("存档缺少场景名称。");
        save.Resources = save.Resources ?? new ResourceSaveData();
        save.Residents = save.Residents ?? new List<ResidentSaveData>();
        save.Buildings = save.Buildings ?? new List<BuildingSaveData>();
        save.Inventory = save.Inventory ?? new InventorySaveData();
        save.DeployedMechs = save.DeployedMechs ?? new List<MechSaveData>();
        MigrateToCurrentVersion(save);
        save.Inventory.ComponentWarehouse = save.Inventory.ComponentWarehouse ?? new List<ComponentStackSaveData>();
        save.Inventory.ChassisWarehouse = save.Inventory.ChassisWarehouse ?? new List<ChassisStackSaveData>();
        save.Inventory.Components = save.Inventory.Components ?? new List<InstancedComponentSaveData>();
        save.Inventory.Chassis = save.Inventory.Chassis ?? new List<InstancedChassisSaveData>();
        save.Inventory.Accessories = save.Inventory.Accessories ?? new List<InstancedAccessorySaveData>();
        foreach (ResidentSaveData resident in save.Residents)
            resident.TraitIDs = resident.TraitIDs ?? new List<string>();
        foreach (BuildingSaveData building in save.Buildings)
        {
            building.StaffResidentIDs = building.StaffResidentIDs ?? new List<string>();
            building.ProductionQueue = building.ProductionQueue ?? new List<ProductionTaskSaveData>();
            foreach (ProductionTaskSaveData task in building.ProductionQueue)
                task.CraftedByResidentIDs = task.CraftedByResidentIDs ?? new List<string>();
        }
        foreach (InstancedComponentSaveData component in save.Inventory.Components)
        {
            component.SocketedAccessoryIDs = component.SocketedAccessoryIDs ?? new List<string>();
            component.RolledStats = component.RolledStats ?? new List<StatEntry>();
            component.Affixes = component.Affixes ?? new List<ComponentAffixInstance>();
            component.CraftedByResidentIDs = component.CraftedByResidentIDs ?? new List<string>();
            foreach (ComponentAffixInstance affix in component.Affixes)
                if (affix != null) affix.Modifiers = affix.Modifiers ?? new List<StatEntry>();
        }
        foreach (MechSaveData mech in save.DeployedMechs)
        {
            mech.SlotIndices = mech.SlotIndices ?? new List<int>();
            mech.EquippedComponentIDs = mech.EquippedComponentIDs ?? new List<string>();
        }
    }

    private static void MigrateToCurrentVersion(GameSaveData save)
    {
        while (save.Version < GameSaveData.CurrentVersion)
        {
            switch (save.Version)
            {
                case 1:
                    foreach (ResidentSaveData resident in save.Residents)
                    {
                        resident.Discipline = 0.5f;
                        resident.Sociability = 0.5f;
                        resident.Courage = 0.5f;
                    }
                    save.Version = 2;
                    break;
                case 2:
                    save.Inventory = save.Inventory ?? new InventorySaveData();
                    save.Inventory.Components = save.Inventory.Components ?? new List<InstancedComponentSaveData>();
                    save.Inventory.ComponentWarehouse = save.Inventory.ComponentWarehouse ?? new List<ComponentStackSaveData>();
                    foreach (InstancedComponentSaveData component in save.Inventory.Components)
                    {
                        component.Quality = ComponentQuality.Standard;
                        component.RolledStats = component.RolledStats ?? new List<StatEntry>();
                        component.Affixes = component.Affixes ?? new List<ComponentAffixInstance>();
                        component.CraftedByResidentIDs = component.CraftedByResidentIDs ?? new List<string>();
                    }
                    foreach (ComponentStackSaveData stack in save.Inventory.ComponentWarehouse)
                    {
                        for (int i = 0; i < stack.Quantity; i++)
                        {
                            save.Inventory.Components.Add(new InstancedComponentSaveData
                            {
                                InstanceID = Guid.NewGuid().ToString(),
                                DefinitionID = stack.DefinitionID,
                                CurrentMark = stack.Level,
                                Quality = ComponentQuality.Standard
                            });
                        }
                    }
                    save.Inventory.ComponentWarehouse.Clear();
                    save.Version = 3;
                    break;
                default:
                    throw new InvalidDataException($"缺少从版本 {save.Version} 开始的存档迁移规则。");
            }
        }
    }

    private static void WriteAtomically(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        File.WriteAllText(temporaryPath, contents);
        if (File.Exists(path))
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Replace(temporaryPath, path, backupPath);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }
}
