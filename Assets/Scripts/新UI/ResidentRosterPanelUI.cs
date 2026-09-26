using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResidentRosterPanelUI : MonoBehaviour
{
    private const string PrefabResourcePath = "UI/ResidentRosterPanel";

    private enum RosterFilter { All, Idle, Working }

    public static ResidentRosterPanelUI Instance { get; private set; }

    [Header("=== 自动生成的界面引用 ===")]
    public Button BackdropButton;
    public Button CloseButton;
    public Button FilterButton;
    public TMP_Text FilterButtonText;
    public TMP_Text TitleText;
    public TMP_Text SummaryText;
    public TMP_Text EmptyText;
    public RectTransform ListContent;
    public ResidentRosterRowUI RowTemplate;

    private BuildingBase targetBuilding;
    private RosterFilter currentFilter;

    public static void OpenRoster()
    {
        EnsureInstance()?.Show(null);
    }

    public static void OpenForBuilding(BuildingBase building)
    {
        if (building == null || !building.SupportsStaff)
        {
            UIFeedback.Show("该建筑不支持居民岗位。");
            return;
        }
        EnsureInstance()?.Show(building);
    }

    private static ResidentRosterPanelUI EnsureInstance()
    {
        if (Instance != null) return Instance;
        Instance = FindObjectOfType<ResidentRosterPanelUI>(true);
        if (Instance != null) return Instance;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError("未找到自动生成的居民名册 UI。请执行 Tools/Chimera/重新生成居民名册UI。");
            UIFeedback.Show("居民名册 UI 尚未生成。");
            return null;
        }

        GameObject instanceObject = Instantiate(prefab);
        instanceObject.name = "ResidentRosterPanel";
        Instance = instanceObject.GetComponent<ResidentRosterPanelUI>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (RowTemplate != null) RowTemplate.gameObject.SetActive(false);
        if (BackdropButton != null) BackdropButton.onClick.AddListener(Close);
        if (CloseButton != null) CloseButton.onClick.AddListener(Close);
        if (FilterButton != null) FilterButton.onClick.AddListener(CycleFilter);
    }

    private void OnEnable()
    {
        if (PopulationManager.Instance != null)
            PopulationManager.Instance.OnPopulationChanged += RefreshRows;
    }

    private void OnDisable()
    {
        if (PopulationManager.Instance != null)
            PopulationManager.Instance.OnPopulationChanged -= RefreshRows;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Show(BuildingBase building)
    {
        targetBuilding = building;
        currentFilter = building != null ? RosterFilter.Idle : RosterFilter.All;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshRows();
    }

    public void Close()
    {
        targetBuilding = null;
        gameObject.SetActive(false);
    }

    private void CycleFilter()
    {
        if (targetBuilding != null) return;
        currentFilter = (RosterFilter)(((int)currentFilter + 1) % 3);
        RefreshRows();
    }

    private void RefreshRows()
    {
        if (!isActiveAndEnabled || ListContent == null || RowTemplate == null) return;

        for (int i = ListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = ListContent.GetChild(i);
            if (child == RowTemplate.transform) continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        PopulationManager population = PopulationManager.Instance;
        List<ResidentData> residents = population != null
            ? population.TotalResidents.Where(x => x != null).ToList()
            : new List<ResidentData>();

        int idleCount = residents.Count(x => x.Status == ResidentStatus.Idle);
        int travelingCount = residents.Count(x => x.Status == ResidentStatus.TravelingToWork);
        int workingCount = residents.Count(x => x.Status == ResidentStatus.Working);

        if (targetBuilding != null)
        {
            residents = residents
                .Where(x => x.Status == ResidentStatus.Idle)
                .OrderByDescending(GetContribution)
                .ThenBy(x => x.ResidentName)
                .ToList();
            int reserved = targetBuilding.GetReservedStaffCount();
            int occupied = targetBuilding.GetStaffList().Count;
            if (TitleText != null) TitleText.text = $"派遣居民 · {targetBuilding.BuildingName}";
            if (SummaryText != null)
                SummaryText.text = $"岗位 {occupied + reserved}/{targetBuilding.MaxStaffCapacity}   ·   可派遣 {residents.Count} 人";
            if (FilterButton != null) FilterButton.interactable = false;
            if (FilterButtonText != null) FilterButtonText.text = "仅显示赋闲";
        }
        else
        {
            residents = ApplyFilter(residents)
                .OrderBy(x => GetStatusOrder(x.Status))
                .ThenBy(x => x.ResidentName)
                .ToList();
            if (TitleText != null) TitleText.text = "殖民地居民名册";
            if (SummaryText != null)
                SummaryText.text = $"总人口 {population?.TotalResidents.Count ?? 0}   ·   赋闲 {idleCount}   ·   前往工作 {travelingCount}   ·   在职 {workingCount}";
            if (FilterButton != null) FilterButton.interactable = true;
            if (FilterButtonText != null) FilterButtonText.text = GetFilterLabel();
        }

        foreach (ResidentData resident in residents)
        {
            ResidentRosterRowUI row = Instantiate(RowTemplate, ListContent);
            row.gameObject.SetActive(true);
            BindRow(row, resident);
        }

        if (EmptyText != null)
        {
            EmptyText.gameObject.SetActive(residents.Count == 0);
            EmptyText.text = targetBuilding != null ? "当前没有可派遣的赋闲居民" : "当前筛选条件下没有居民";
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(ListContent);
    }

    private IEnumerable<ResidentData> ApplyFilter(IEnumerable<ResidentData> residents)
    {
        switch (currentFilter)
        {
            case RosterFilter.Idle:
                return residents.Where(x => x.Status == ResidentStatus.Idle || x.Status == ResidentStatus.TravelingToWork);
            case RosterFilter.Working:
                return residents.Where(x => x.Status == ResidentStatus.Working || x.Status == ResidentStatus.Piloting);
            default:
                return residents;
        }
    }

    private void BindRow(ResidentRosterRowUI row, ResidentData resident)
    {
        ResidentIdentityLibrarySO library = PopulationManager.Instance != null
            ? PopulationManager.Instance.IdentityLibrary
            : null;
        string traits = ResidentWorkCalculator.GetTraitSummary(resident, library);
        string status = GetStatusLabel(resident);
        float contribution = GetContribution(resident);
        string detail = targetBuilding != null
            ? $"目标生产力  {contribution:0.00}    纪律  {resident.Discipline:P0}    特性  {traits}"
            : $"科技 {resident.TechProficiency:0.00}    血肉 {resident.FleshProficiency:0.00}    魔力 {resident.ManaProficiency:0.00}    特性  {traits}";

        bool assignmentMode = targetBuilding != null;
        bool entityExists = FindResidentEntity(resident) != null;
        bool canAssign = assignmentMode && entityExists && targetBuilding.CanAcceptStaffOrder(resident);
        string actionLabel = assignmentMode ? (canAssign ? "派遣" : "不可派遣") : "查看";
        bool actionEnabled = assignmentMode ? canAssign : CanInspect(resident);

        row.Bind(
            resident.ResidentName,
            $"Lv.{resident.Level}   ·   {status}",
            detail,
            actionLabel,
            actionEnabled,
            () => HandleRowAction(resident));
    }

    private void HandleRowAction(ResidentData resident)
    {
        if (targetBuilding != null)
        {
            if (!targetBuilding.CanAcceptStaffOrder(resident))
            {
                UIFeedback.Show("岗位已满，或居民当前无法接受派遣。");
                RefreshRows();
                return;
            }
            ResidentEntity entity = FindResidentEntity(resident);
            if (entity == null)
            {
                UIFeedback.Show("未找到该居民的世界实体。");
                RefreshRows();
                return;
            }
            entity.OrderGarrison(targetBuilding);
            UIFeedback.Show($"已派遣 {resident.ResidentName} 前往 {targetBuilding.BuildingName}。");
            RefreshRows();
            return;
        }

        ResidentEntity worldEntity = FindResidentEntity(resident);
        if (worldEntity != null)
        {
            if (SelectionContextHUD.Instance != null) SelectionContextHUD.Instance.Refresh(worldEntity);
            CenterCameraOn(worldEntity.transform.position);
            Close();
            return;
        }

        BuildingBase building = FindCarrierBuilding(resident.CurrentCarrierID);
        if (building != null)
        {
            if (SelectionContextHUD.Instance != null) SelectionContextHUD.Instance.Refresh(building);
            CenterCameraOn(building.transform.position);
            Close();
        }
    }

    private float GetContribution(ResidentData resident)
    {
        if (targetBuilding is FactoryBuilding factory) return factory.GetResidentContribution(resident);
        ResidentIdentityLibrarySO library = PopulationManager.Instance != null
            ? PopulationManager.Instance.IdentityLibrary
            : null;
        return ResidentWorkCalculator.CalculateContribution(resident, ResidentWorkDomain.General, library);
    }

    private static ResidentEntity FindResidentEntity(ResidentData resident)
    {
        if (resident == null) return null;
        return FindObjectsOfType<ResidentEntity>().FirstOrDefault(x => x != null &&
            (x.MyData == resident || (x.MyData != null && x.MyData.InstanceID == resident.InstanceID)));
    }

    private static BuildingBase FindCarrierBuilding(string persistentID)
    {
        if (string.IsNullOrWhiteSpace(persistentID)) return null;
        return BuildingBase.AllPlacedBuildings.FirstOrDefault(x => x != null && x.PersistentID == persistentID);
    }

    private static bool CanInspect(ResidentData resident)
    {
        return FindResidentEntity(resident) != null || FindCarrierBuilding(resident.CurrentCarrierID) != null;
    }

    private static void CenterCameraOn(Vector3 position)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        Transform target = camera.transform.parent != null ? camera.transform.parent : camera.transform;
        Vector3 current = target.position;
        target.position = new Vector3(position.x, position.y, current.z);
    }

    private static int GetStatusOrder(ResidentStatus status)
    {
        switch (status)
        {
            case ResidentStatus.Idle: return 0;
            case ResidentStatus.TravelingToWork: return 1;
            case ResidentStatus.Working: return 2;
            default: return 3;
        }
    }

    private static string GetStatusLabel(ResidentData resident)
    {
        switch (resident.Status)
        {
            case ResidentStatus.Working:
                BuildingBase building = FindCarrierBuilding(resident.CurrentCarrierID);
                return building != null ? $"在 {building.BuildingName} 工作" : "工作中";
            case ResidentStatus.Piloting: return "驾驶中";
            case ResidentStatus.TravelingToWork: return "前往工作";
            default: return "赋闲";
        }
    }

    private string GetFilterLabel()
    {
        switch (currentFilter)
        {
            case RosterFilter.Idle: return "赋闲 / 途中";
            case RosterFilter.Working: return "在职 / 驾驶";
            default: return "全部居民";
        }
    }
}
