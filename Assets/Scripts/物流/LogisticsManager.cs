using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Single authoritative location ledger. Reservations are derived from live jobs, never duplicated counters.</summary>
public sealed class LogisticsManager : MonoBehaviour
{
    public static LogisticsManager Instance { get; private set; }
    public LogisticsSaveData Data = new LogisticsSaveData();
    public bool Ready { get; private set; }
    public event Action Changed;
    private float nextTick;
    private readonly Dictionary<string, float> workerRetry = new Dictionary<string, float>();
    private readonly Dictionary<string, float> blockedDestinations = new Dictionary<string, float>();
    private readonly Dictionary<string, GameObject> recoveryMarkers = new Dictionary<string, GameObject>();
    private const float TickInterval = .25f;
    public static string InputID(ProductionTask task) => "order:" + task.TaskID;
    public static string OutputID(FactoryBuilding factory) => "output:" + factory.PersistentID;
    public static string BagID(string residentID) => "bag:" + residentID;
    public static bool Loading => SaveGameManager.Instance != null && SaveGameManager.Instance.IsLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }
    public static LogisticsManager EnsureInstance()
    {
        if (Instance == null) new GameObject("自动物流").AddComponent<LogisticsManager>();
        return Instance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update()
    {
        if (Loading || Time.time < nextTick) return;
        nextTick = Time.time + TickInterval;
        if (!Ready)
        {
            if (GlobalResourceManager.Instance == null || PlayerInventoryManager.Instance == null || RTSGridSystem.Instance == null) return;
            InitializeLegacy();
        }
        Tick();
    }

    public LogisticsStorage Get(string id) => Data.Storages.Find(x => x.ID == id);
    public IEnumerable<LogisticsStorage> Warehouses => Data.Storages.Where(x => x.Kind == StorageKind.Warehouse);
    public float ReservedOut(string id, string key) => Data.Jobs.Where(x => !x.PickedUp && x.SourceID == id && x.Key == key).Sum(x => x.Amount);
    public float ReservedIn(string id) => Data.Jobs.Where(x => x.TargetID == id).Sum(x => x.Amount);
    public float Available(LogisticsStorage store, string key) => Mathf.Max(0, store.Count(key) - ReservedOut(store.ID, key));
    public float Free(LogisticsStorage store) => Mathf.Max(0, store.Capacity - store.Used - ReservedIn(store.ID));
    public float AvailableResource(string key) => Warehouses.Sum(x => Available(x, key));
    public LogisticsStorage Locate(string key) => Data.Storages.Find(x => x.Count(key) > 0);
    public string LocationLabel(string key)
    {
        var storage = Locate(key);
        if (storage == null) return "未入库";
        string state = storage.Kind == StorageKind.Backpack ? "运输中" :
            ReservedOut(storage.ID, key) > 0 ? "已预留" : storage.Kind == StorageKind.FactoryOutput ? "待入库" : "可用";
        return storage.Name + " · " + state;
    }
    public bool ItemAvailable(string key)
    {
        var store = Locate(key);
        return store != null && store.Kind == StorageKind.Warehouse && Available(store, key) >= 1;
    }
    public bool ConsumeItem(string key)
    {
        var store = Warehouses.FirstOrDefault(x => Available(x, key) >= 1);
        if (store == null || !store.Remove(key, 1)) return false;
        Notify(); return true;
    }
    // Used only by the existing assembly editor's synchronous rollback transaction.
    public void ReclaimEquippedComponent(string instanceID)
    {
        string key = "component:" + instanceID;
        foreach (var job in Data.Jobs.Where(x => x.Key == key).ToArray()) CancelJob(job);
        foreach (var storage in Data.Storages) storage.Cargo.RemoveAll(x => x.Key == key);
        Notify();
    }

    public LogisticsStorage RegisterWarehouse(WarehouseBuilding building)
    {
        var storage = Get(building.PersistentID);
        if (storage != null) return storage;
        storage = new LogisticsStorage { ID = building.PersistentID, OwnerID = building.PersistentID,
            Kind = StorageKind.Warehouse, Name = building.BuildingName, Capacity = building.StorageCapacity,
            Position = new SerializableVector3(building.GetInteractionPoint()) };
        Data.Storages.Add(storage);
        return storage;
    }
    public LogisticsStorage EnsureOutput(FactoryBuilding factory)
    {
        var store = Get(OutputID(factory));
        if (store != null) return store;
        store = new LogisticsStorage { ID = OutputID(factory), OwnerID = factory.PersistentID, Kind = StorageKind.FactoryOutput,
            Name = factory.BuildingName + "出货区", Capacity = factory.OutputCapacity,
            Position = new SerializableVector3(factory.GetInteractionPoint()) };
        Data.Storages.Add(store); return store;
    }
    public LogisticsStorage EnsureInput(FactoryBuilding factory, ProductionTask task)
    {
        var store = Get(InputID(task));
        if (store != null) return store;
        store = new LogisticsStorage { ID = InputID(task), OwnerID = factory.PersistentID, Kind = StorageKind.OrderInput,
            Name = factory.BuildingName + " / " + task.ItemName + "原料", Capacity = LogisticsKeys.Resources(task.PaidCost).Sum(x => x.Amount),
            Position = new SerializableVector3(factory.GetInteractionPoint()) };
        Data.Storages.Add(store);
        // Old saves already paid their queue costs. Restore those materials once, at the factory.
        if (!task.UsesLogistics)
        {
            foreach (var cargo in LogisticsKeys.Resources(task.PaidCost)) store.Add(cargo.Key, cargo.Amount);
            task.UsesLogistics = true;
        }
        return store;
    }
    public LogisticsStorage EnsureBag(ResidentEntity worker)
    {
        string id = BagID(worker.MyData.InstanceID);
        var bag = Get(id);
        if (bag == null)
        {
            bag = new LogisticsStorage { ID = id, OwnerID = worker.MyData.InstanceID, Kind = StorageKind.Backpack,
                Name = worker.MyData.ResidentName + "背包", Capacity = worker.MyData.CarryCapacity };
            Data.Storages.Add(bag);
        }
        bag.Position = new SerializableVector3(worker.transform.position);
        return bag;
    }
    private LogisticsStorage Recovery(Vector3 position, string name)
    {
        var store = new LogisticsStorage { ID = "recovery:" + Guid.NewGuid(), Kind = StorageKind.Recovery,
            Name = name, Capacity = float.MaxValue, Position = new SerializableVector3(position) };
        Data.Storages.Add(store);
        return store;
    }
    public void Deposit(string key, float amount)
    {
        if (amount <= 0) return;
        foreach (var store in Warehouses.Where(x => x.Accepts(key)).ToArray())
        {
            float move = Mathf.Min(amount, Free(store));
            store.Add(key, move); amount -= move;
            if (amount <= .0001f) break;
        }
        if (amount > .0001f)
        {
            var first = Warehouses.FirstOrDefault();
            Recovery(first != null ? first.Position.ToVector3() : Vector3.zero, "待收纳补给").Add(key, amount);
            UIFeedback.Show("仓库容量不足或拒收，补给已保留在待收纳货物中。");
        }
        Notify();
    }
    public void DepositResources(ResourceSet resources)
    {
        if (!LogisticsKeys.Valid(resources)) return;
        foreach (var cargo in LogisticsKeys.Resources(resources)) Deposit(cargo.Key, cargo.Amount);
    }
    public bool ConsumeResources(ResourceSet cost)
    {
        if (!LogisticsKeys.Valid(cost) || LogisticsKeys.Resources(cost).Any(x => AvailableResource(x.Key) + .0001f < x.Amount)) return false;
        foreach (var cargo in LogisticsKeys.Resources(cost))
        {
            float remaining = cargo.Amount;
            foreach (var store in Warehouses)
            {
                float take = Mathf.Min(remaining, Available(store, cargo.Key));
                if (take > 0) store.Remove(cargo.Key, take);
                remaining -= take;
            }
        }
        Notify(); return true;
    }
    public void InitializeLegacy()
    {
        if (Ready) return;
        foreach (var warehouse in BuildingBase.AllPlacedBuildings.OfType<WarehouseBuilding>()) RegisterWarehouse(warehouse);
        if (!Warehouses.Any())
        {
            var warehouse = WarehouseBuilding.CreateInitial();
            if (warehouse == null) return;
            RegisterWarehouse(warehouse);
        }
        var resources = GlobalResourceManager.Instance;
        var initial = Warehouses.First();
        // Migration must not lose an over-capacity legacy inventory.
        initial.Capacity = Mathf.Max(initial.Capacity, resources.CurrentScrap + resources.CurrentBiomass + resources.CurrentManaStone +
            PlayerInventoryManager.Instance.ComponentInventory.Count + PlayerInventoryManager.Instance.GetChassisStacks().Sum(x => x.Quantity) + 100);
        foreach (var cargo in LogisticsKeys.Resources(new ResourceSet(resources.CurrentScrap, resources.CurrentBiomass, resources.CurrentManaStone))) initial.Add(cargo.Key, cargo.Amount);
        foreach (var stack in PlayerInventoryManager.Instance.GetChassisStacks()) initial.Add("chassis:" + stack.BaseData.ChassisID, stack.Quantity);
        foreach (var component in PlayerInventoryManager.Instance.ComponentInventory.Where(x => x != null && !x.IsEquipped))
            initial.Add("component:" + component.InstanceID, 1);
        Ready = true;
        EnsureUI();
        foreach (var factory in BuildingBase.AllPlacedBuildings.OfType<FactoryBuilding>())
            foreach (var task in factory.TaskQueue) EnsureInput(factory, task);
        Notify();
    }

    public bool TryStartProduction(FactoryBuilding factory, ProductionTask task)
    {
        if (!Ready) { task.LogisticsStatus = "物流初始化中"; return false; }
        var output = EnsureOutput(factory);
        int productionReservations = factory.TaskQueue.Count(x => x.MaterialsConsumed);
        if (!task.MaterialsConsumed && output.Used + productionReservations >= output.Capacity)
        { task.LogisticsStatus = "出货区已满，等待搬运"; return false; }
        if (task.MaterialsConsumed) return true;
        var input = EnsureInput(factory, task);
        float required = LogisticsKeys.Resources(task.PaidCost).Sum(x => x.Amount);
        if (required > factory.InputCapacity)
        { task.LogisticsStatus = "配方超出原料区容量"; return false; }
        if (LogisticsKeys.Resources(task.PaidCost).Any(x => input.Count(x.Key) + .0001f < x.Amount))
        {
            task.LogisticsStatus = $"原料 {input.Used:0.#}/{required:0.#} · 在途 {ReservedIn(input.ID):0.#}";
            return false;
        }
        foreach (var cargo in LogisticsKeys.Resources(task.PaidCost)) input.Remove(cargo.Key, cargo.Amount);
        task.MaterialsConsumed = true;
        task.LogisticsStatus = "生产中";
        Notify(); return true;
    }
    public void CompleteProduction(FactoryBuilding factory, ProductionTask task, string key)
    {
        EnsureOutput(factory).Add(key, 1);
        Data.Storages.RemoveAll(x => x.ID == InputID(task) && x.Used == 0);
        Notify();
    }
    public void CancelOrder(FactoryBuilding factory, ProductionTask task)
    {
        var input = EnsureInput(factory, task);
        foreach (var job in Data.Jobs.Where(x => x.OrderID == task.TaskID).ToArray()) CancelJob(job);
        if (task.MaterialsConsumed)
            foreach (var cargo in LogisticsKeys.Resources(task.PaidCost)) input.Add(cargo.Key, cargo.Amount);
        input.Kind = StorageKind.Recovery;
        input.Name = factory.BuildingName + "退料区";
        task.MaterialsConsumed = false;
        if (input.Used == 0) Data.Storages.Remove(input);
        Notify();
    }

    public void Tick()
    {
        if (!Ready) return;
        var workers = ResidentEntity.ActiveResidents.Where(x => x != null && x.MyData != null).ToArray();
        foreach (var worker in workers) EnsureBag(worker);
        ValidateJobs(workers);
        // Plan material requests first; deliveries reserve factory buffer space across all orders.
        foreach (var factory in BuildingBase.AllPlacedBuildings.OfType<FactoryBuilding>().Where(x => x != null))
        {
            EnsureOutput(factory);
            int planned = 0;
            foreach (var task in factory.TaskQueue.Where(x => !x.IsPaused))
            {
                if (planned++ >= factory.ActiveProductionLineCount) break;
                var input = EnsureInput(factory, task);
                if (task.MaterialsConsumed || input.Capacity > factory.InputCapacity) continue;
                // Reserve enough buffer for an entire recipe before admitting its first delivery.
                // Partial recipes across parallel lines must not fill the buffer and deadlock each other.
                float committed = Data.Storages.Where(x => x.ID != input.ID && x.OwnerID == factory.PersistentID &&
                    x.Kind == StorageKind.OrderInput && x.Used + ReservedIn(x.ID) > .0001f).Sum(x => x.Capacity);
                if (committed + input.Capacity > factory.InputCapacity) continue;
                foreach (var cost in LogisticsKeys.Resources(task.PaidCost))
                {
                    float missing = cost.Amount - input.Count(cost.Key) - Data.Jobs.Where(x => x.TargetID == input.ID && x.Key == cost.Key).Sum(x => x.Amount);
                    float bufferFree = factory.InputCapacity - Data.Storages.Where(x => x.OwnerID == factory.PersistentID && x.Kind == StorageKind.OrderInput)
                        .Sum(x => x.Used + ReservedIn(x.ID));
                    foreach (var source in Warehouses.OrderBy(x => Vector3.SqrMagnitude(x.Position.ToVector3() - input.Position.ToVector3())))
                    {
                        float amount = Mathf.Min(missing, Available(source, cost.Key), bufferFree);
                        if (amount <= .0001f) continue;
                        AddJob(source, input, cost.Key, amount, task.TaskID);
                        missing -= amount; bufferFree -= amount;
                    }
                }
            }
        }
        // Output and cancelled material are collected by any available hauler.
        foreach (var source in Data.Storages.Where(x => x.Kind == StorageKind.FactoryOutput || x.Kind == StorageKind.Recovery).ToArray())
            foreach (var cargo in source.Cargo.ToArray())
                PlanReturn(source, cargo.Key, Available(source, cargo.Key));

        int pathBudget = 4;
        foreach (var worker in workers)
        {
            var data = worker.MyData;
            var bag = EnsureBag(worker);
            var job = Data.Jobs.Find(x => x.WorkerID == data.InstanceID);
            if (data.Status != ResidentStatus.Idle || !data.HaulingEnabled || worker.ManualMoveActive || worker.LogisticsHoldUntil > Time.time)
            {
                if (job != null) CancelJob(job);
                continue;
            }
            if (workerRetry.TryGetValue(data.InstanceID, out float retry) && Time.time < retry) continue;
            if (job == null && bag.Used > 0)
            {
                var cargo = bag.Cargo[0];
                job = PlanReturn(bag, cargo.Key, cargo.Amount, data.InstanceID);
            }
            if (job == null && bag.Used == 0)
            {
                job = Data.Jobs.Where(x => string.IsNullOrEmpty(x.WorkerID) && x.RetryAt <= Time.time)
                    .OrderBy(x => x.OrderID == null ? 1 : 0)
                    .ThenBy(x => Vector3.SqrMagnitude((Get(x.SourceID)?.Position.ToVector3() ?? worker.transform.position) - worker.transform.position)).FirstOrDefault();
                if (job != null)
                {
                    if (pathBudget <= 0) break;
                    float capacity = Mathf.Max(1, data.CarryCapacity);
                    if (job.Amount > capacity)
                    {
                        var remainder = new HaulJob { SourceID = job.SourceID, TargetID = job.TargetID, Key = job.Key,
                            Amount = job.Amount - capacity, OrderID = job.OrderID };
                        job.Amount = capacity; Data.Jobs.Add(remainder);
                    }
                    job.WorkerID = data.InstanceID;
                }
            }
            if (job != null) Advance(worker, job, ref pathBudget);
        }
        Data.Storages.RemoveAll(x => x.Kind == StorageKind.Recovery && x.Used <= 0 &&
            !Data.Jobs.Any(j => j.SourceID == x.ID || j.TargetID == x.ID));
        SyncRecoveryMarkers();
        Notify();
    }
    private HaulJob AddJob(LogisticsStorage source, LogisticsStorage target, string key, float amount, string orderID = null)
    {
        if (source.ID == target.ID || !target.Accepts(key)) return null;
        amount = Mathf.Min(amount, Available(source, key), Free(target));
        if (amount <= .0001f) return null;
        var job = new HaulJob { SourceID = source.ID, TargetID = target.ID, Key = key, Amount = amount, OrderID = orderID };
        Data.Jobs.Add(job); return job;
    }
    private HaulJob PlanReturn(LogisticsStorage source, string key, float amount, string workerID = null)
    {
        if (amount <= .0001f) return null;
        foreach (var target in Warehouses.Where(x => x.Accepts(key) && Free(x) > .0001f)
            .Where(x => workerID == null || !blockedDestinations.TryGetValue(workerID + "|" + x.ID, out float until) || Time.time >= until)
            .OrderBy(x => Vector3.SqrMagnitude(x.Position.ToVector3() - source.Position.ToVector3())))
        {
            var job = AddJob(source, target, key, amount);
            if (job == null) continue;
            if (workerID != null) { job.PickedUp = true; job.WorkerID = workerID; }
            return job;
        }
        return null;
    }
    private void Advance(ResidentEntity worker, HaulJob job, ref int pathBudget)
    {
        var target = Get(job.PickedUp ? job.TargetID : job.SourceID);
        if (target == null) { CancelJob(job); return; }
        Vector3 point = target.Position.ToVector3();
        if (Vector2.Distance(worker.transform.position, point) <= .32f)
        {
            worker.StopLogisticsMovement(); job.Moving = false;
            var bag = EnsureBag(worker);
            if (!job.PickedUp)
            {
                if (bag.Capacity - bag.Used + .0001f < job.Amount || !target.Remove(job.Key, job.Amount)) { CancelJob(job); return; }
                bag.Add(job.Key, job.Amount); job.PickedUp = true;
            }
            else
            {
                if (target.Capacity - target.Used + .0001f < job.Amount || !target.Accepts(job.Key) || !bag.Remove(job.Key, job.Amount))
                { CancelJob(job); return; }
                target.Add(job.Key, job.Amount);
                Data.Jobs.Remove(job);
            }
            PlayerInventoryManager.Instance?.ForceTriggerInventoryEvent();
            return;
        }
        if (!job.Moving)
        {
            if (pathBudget <= 0) return;
            pathBudget--;
            if (!worker.TryLogisticsMove(point)) { FailRoute(worker, job); return; }
            job.Moving = true; job.LastPosition = worker.transform.position; job.Stalled = 0;
        }
        if (Vector2.Distance(worker.transform.position, job.LastPosition) > .1f)
        { job.LastPosition = worker.transform.position; job.Stalled = 0; }
        else job.Stalled += TickInterval;
        if (job.Stalled > 8) FailRoute(worker, job);
    }
    private void FailRoute(ResidentEntity worker, HaulJob job)
    {
        workerRetry[worker.MyData.InstanceID] = Time.time + 5;
        blockedDestinations[worker.MyData.InstanceID + "|" + (job.PickedUp ? job.TargetID : job.SourceID)] = Time.time + 20;
        worker.LogisticsIssue = "通道受阻，稍后重试";
        worker.StopLogisticsMovement();
        if (job.PickedUp) CancelJob(job);
        else { job.WorkerID = null; job.Moving = false; job.RetryAt = Time.time + 5; }
    }
    private void CancelJob(HaulJob job)
    {
        var worker = ResidentEntity.ActiveResidents.FirstOrDefault(x => x != null && x.MyData?.InstanceID == job.WorkerID);
        if (worker != null) worker.StopLogisticsMovement();
        // After pickup the cargo stays in the backpack. A return job will be created when work resumes.
        Data.Jobs.Remove(job);
    }
    private void ValidateJobs(ResidentEntity[] workers)
    {
        foreach (var job in Data.Jobs.ToArray())
        {
            var target = Get(job.TargetID);
            bool missingOrder = !string.IsNullOrEmpty(job.OrderID) && !BuildingBase.AllPlacedBuildings.OfType<FactoryBuilding>()
                .Any(f => f != null && f.TaskQueue.Any(t => t.TaskID == job.OrderID));
            if (target == null || !target.Accepts(job.Key) || Get(job.SourceID) == null || missingOrder)
            { CancelJob(job); continue; }
            if (!string.IsNullOrEmpty(job.WorkerID) && !workers.Any(x => x.MyData.InstanceID == job.WorkerID))
                CancelJob(job);
        }
    }
    public void Interrupt(ResidentEntity worker)
    {
        if (worker.MyData == null) return;
        foreach (var job in Data.Jobs.Where(x => x.WorkerID == worker.MyData.InstanceID).ToArray()) CancelJob(job);
        worker.LogisticsHoldUntil = Time.time + 2;
        worker.LogisticsIssue = null;
        Notify();
    }
    public void ReleaseResident(ResidentEntity worker)
    {
        if (Loading || !Ready || worker.MyData == null) return;
        Interrupt(worker);
        var bag = Get(BagID(worker.MyData.InstanceID));
        if (bag == null || bag.Used == 0) return;
        // Garrison has no world entity; preserve cargo at its last safe location for other haulers.
        var recovery = Recovery(worker.transform.position, worker.MyData.ResidentName + "遗留货物");
        recovery.Cargo.AddRange(bag.Cargo); bag.Cargo.Clear();
        Data.Storages.Remove(bag);
        Notify();
    }
    public void ReleaseBuilding(BuildingBase building)
    {
        if (!Ready || Loading || !Application.isPlaying) return;
        if (building is FactoryBuilding factory)
            foreach (var task in factory.TaskQueue.ToArray()) CancelOrder(factory, task);
        foreach (var store in Data.Storages.Where(x => x.OwnerID == building.PersistentID).ToArray())
        {
            foreach (var job in Data.Jobs.Where(x => x.SourceID == store.ID || x.TargetID == store.ID).ToArray()) CancelJob(job);
            store.OwnerID = null; store.Kind = StorageKind.Recovery; store.Name = building.BuildingName + "遗留货物";
            store.Capacity = float.MaxValue;
        }
        Notify();
    }
    public string WorkerSummary(ResidentData resident)
    {
        var bag = Get(BagID(resident.InstanceID));
        var job = Data.Jobs.Find(x => x.WorkerID == resident.InstanceID);
        string activity = job != null ? (job.PickedUp ? "交付 " : "取货 ") + LogisticsKeys.Name(job.Key) + " → " + Get(job.PickedUp ? job.TargetID : job.SourceID)?.Name
            : resident.Status != ResidentStatus.Idle ? "已分配其他岗位" : bag != null && bag.Used > 0 ? "携货待安排 / 等待接收空间" : resident.HaulingEnabled ? "等待可执行物流任务" : "搬运关闭";
        return activity + $"  |  负重 {bag?.Used ?? 0:0.#}/{resident.CarryCapacity:0.#}";
    }
    public LogisticsSaveData Capture()
    {
        foreach (var worker in ResidentEntity.ActiveResidents.Where(x => x != null && x.MyData != null)) EnsureBag(worker);
        return JsonUtility.FromJson<LogisticsSaveData>(JsonUtility.ToJson(Data));
    }
    public void Restore(LogisticsSaveData saved)
    {
        Data = saved ?? new LogisticsSaveData();
        Data.Storages = Data.Storages ?? new List<LogisticsStorage>();
        Data.Jobs = Data.Jobs ?? new List<HaulJob>();
        workerRetry.Clear();
        blockedDestinations.Clear();
        if (saved == null) { Ready = false; InitializeLegacy(); return; }
        Ready = true;
        EnsureUI();
        foreach (var job in Data.Jobs) { job.Moving = false; job.RetryAt = 0; job.Stalled = 0; }
        // No duplicate reservation counters to restore: live jobs reconstruct them on demand.
        foreach (var warehouse in BuildingBase.AllPlacedBuildings.OfType<WarehouseBuilding>()) RegisterWarehouse(warehouse);
        foreach (var store in Data.Storages.Where(x => x.Kind != StorageKind.Backpack && x.Kind != StorageKind.Recovery))
            if (!BuildingBase.AllPlacedBuildings.Any(x => x != null && x.PersistentID == store.OwnerID))
            { store.Kind = StorageKind.Recovery; store.Name += "遗留货物"; }
        Notify();
    }
    private void SyncRecoveryMarkers()
    {
        foreach (var id in recoveryMarkers.Keys.ToArray())
            if (Get(id) == null || Get(id).Used <= 0)
            { Destroy(recoveryMarkers[id]); recoveryMarkers.Remove(id); }
        foreach (var store in Data.Storages.Where(x => x.Kind == StorageKind.Recovery && x.Used > 0))
        {
            if (recoveryMarkers.ContainsKey(store.ID)) continue;
            var marker = new GameObject(store.Name);
            marker.transform.position = store.Position.ToVector3();
            marker.transform.localScale = Vector3.one * .4f;
            var renderer = marker.AddComponent<SpriteRenderer>();
            var warehouse = BuildingBase.AllPlacedBuildings.OfType<WarehouseBuilding>().FirstOrDefault();
            if (warehouse != null) renderer.sprite = warehouse.BuildingIcon;
            renderer.color = new Color(1, .78f, .35f); renderer.sortingOrder = 8;
            recoveryMarkers[store.ID] = marker;
        }
    }
    private void EnsureUI()
    {
        if (FindObjectOfType<LogisticsPanelUI>(true) != null) return;
        var prefab = Resources.Load<GameObject>("UI/LogisticsPanel");
        if (prefab != null) Instantiate(prefab);
        else new GameObject("物流界面", typeof(RectTransform)).AddComponent<LogisticsPanelUI>();
    }
    public void Notify()
    {
        if (Ready && GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.SyncLogisticsTotals(AvailableResource(LogisticsKeys.Scrap), AvailableResource(LogisticsKeys.Biomass), AvailableResource(LogisticsKeys.Mana));
        Changed?.Invoke();
    }
}
