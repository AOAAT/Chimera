using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ResidentEntity : MonoBehaviour
{
    [Header("=== 绑定的数据 ===")]
    public ResidentData MyData;
    public static readonly List<ResidentEntity> ActiveResidents = new List<ResidentEntity>();
    public float LogisticsHoldUntil;
    public string LogisticsIssue;
    public bool ManualMoveActive => !logisticsMovement && currentPath != null && pathIndex < currentPath.Count;
    private bool logisticsMovement;

    public bool TryLogisticsMove(Vector3 target)
    {
        var route = GridPathfinder.FindPath(transform.position, target, false);
        if (route == null) return false;
        currentPath = route; pathIndex = 0; logisticsMovement = true; LogisticsIssue = null;
        return true;
    }
    public void StopLogisticsMovement()
    {
        if (!logisticsMovement) return;
        currentPath = null; logisticsMovement = false;
        if (rb != null) rb.velocity = Vector2.zero;
    }

    [Header("=== 物理与移动参数 ===")]
    public float MoveSpeed = 3.5f;
    private Rigidbody2D rb;


    [Header("=== UI 与选中反馈 ===")]
    public GameObject SelectionCircle; // 居民脚下的小光圈


    private List<Vector3> currentPath = null;
    private int pathIndex = 0;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        SetupPhysics();
    }
    private IResidentCarrier targetCarrier; // 当前准备前往的建筑
    private Vector3 workGate;
    private Vector3 lastWorkPosition;
    private float workStallTime;
    private void SetupPhysics()
    {
        gameObject.layer = LayerMask.NameToLayer("Resident");
        rb = GetComponent<Rigidbody2D>();

        // --- 👇 同步注入物理材质 ---
        PhysicsMaterial2D slippery = Resources.Load<PhysicsMaterial2D>("Slippery_Material");
        if (slippery != null) rb.sharedMaterial = slippery;
        // ----------------------------

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = 8f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.radius = 0.2f;
        col.isTrigger = false;
    }

    public void Initialize(ResidentData data, float maxHP)
    {
        MyData = data;
        gameObject.name = $"Resident_{data.ResidentName}";

        // 🌟 [核心修复]：初始化受击躯壳
        var dr = GetComponent<DamageReceiver>();
        if (dr != null)
        {
            // 居民通常没有护甲 (AP = 0)
            dr.Initialize(maxHP, 0);
            dr.isEnemy = false; // 居民永远属于玩家阵营
            if (data.CurrentHP > 0f) dr.CurrentHP = Mathf.Min(data.CurrentHP, dr.MaxHP);
            else data.CurrentHP = dr.CurrentHP;
        }

        SetSelected(false);
        transform.position += (Vector3)UnityEngine.Random.insideUnitCircle * 0.01f;
    }

    public void SetDestination(Vector2 worldPos) // 或者 SetManualMovePoint
    {
        LogisticsManager.Instance?.Interrupt(this);
        logisticsMovement = false;
        // 🌟 核心：在计算新路径前，立即切断当前所有物理惯性
        if (rb != null) rb.velocity = Vector2.zero;

        currentPath = GridPathfinder.FindPath(transform.position, worldPos);
        pathIndex = 0;

        // 如果路径只有1个点（就在脚下），直接清理掉，防止原地抽搐
        if (currentPath != null && currentPath.Count <= 1) currentPath = null;
    }

    public void OrderGarrison(IResidentCarrier carrier)
    {
        if (carrier == null || MyData == null) return;
        if (carrier is BuildingBase building && !building.CanAcceptStaffOrder(MyData))
        {
            UIFeedback.Show($"{building.BuildingName} 的岗位已满，或居民当前无法派遣。");
            return;
        }
        Vector3 gatePos = carrier.GetInteractionPoint();
        List<Vector3> route = GridPathfinder.FindPath(transform.position, gatePos, false);
        if (route == null)
        {
            UIFeedback.Show($"{carrier.GetCarrierName()} 的入口无法到达，请清理通道。");
            return;
        }
        LogisticsManager.Instance?.Interrupt(this);
        logisticsMovement = false;
        targetCarrier = carrier;
        workGate = gatePos;
        lastWorkPosition = transform.position;
        workStallTime = 0f;
        currentPath = route;
        pathIndex = 0;
        if (rb != null) rb.velocity = Vector2.zero;
        MyData.Status = ResidentStatus.TravelingToWork;
        MyData.CurrentCarrierID = carrier is BuildingBase targetBuilding ? targetBuilding.PersistentID : string.Empty;
        if (carrier is BuildingBase reservedBuilding)
            reservedBuilding.NotifyStaffReservationChanged();
        if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();

        // 🔍 DEBUG 2: 确认门的位置
        Debug.Log($"[实体] {MyData.ResidentName} 收到入驻请求。门口世界坐标: {gatePos}");

    }

    public void CancelGarrisonOrder()
    {
        BuildingBase reservedBuilding = targetCarrier as BuildingBase;
        targetCarrier = null;
        currentPath = null;
        if (rb != null) rb.velocity = Vector2.zero;
        if (MyData != null && MyData.Status == ResidentStatus.TravelingToWork)
        {
            MyData.Status = ResidentStatus.Idle;
            MyData.CurrentCarrierID = string.Empty;
            if (reservedBuilding != null) reservedBuilding.NotifyStaffReservationChanged();
            if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();
        }
    }
    private void Update()
    {
        HandleMovement();

        if (targetCarrier != null)
        {
            if (targetCarrier is UnityEngine.Object target && target == null)
            {
                CancelGarrisonOrder();
                return;
            }
            float distToGate = Vector2.Distance(transform.position, workGate);
            if (Vector2.Distance(transform.position, lastWorkPosition) > 0.1f)
            {
                lastWorkPosition = transform.position;
                workStallTime = 0f;
            }
            else workStallTime += Time.deltaTime;
            if (workStallTime > 8f)
            {
                CancelGarrisonOrder();
                UIFeedback.Show("居民前往岗位的通道受阻，已取消派遣并释放岗位。");
                return;
            }

            // 🔍 DEBUG 3: 实时距离监控（如果一直不进门，看这里的数字）
            // 我们改为每隔 0.5 秒打印一次，防止刷屏
            if (Time.frameCount % 30 == 0)
            {
                // Debug.Log($"[实体] {MyData.ResidentName} 距离门口还剩: {distToGate:F2}米");
            }

            if (distToGate < 0.3f) // 🌟 建议从 0.2 调大到 0.3，增加容错
            {
                Debug.Log($"[实体] {MyData.ResidentName} 抵达门口，触发 ExecuteEnterGarrison");
                ExecuteEnterGarrison();
            }
        }
    }
    private void ExecuteEnterGarrison()
    {
        bool success = targetCarrier.TryAddStaff(this.MyData);

        if (success)
        {
            Debug.Log($"<color=green>[实体] {MyData.ResidentName} 入驻成功，执行自我销毁。</color>");
            if (BattleCommandManager.Instance != null)
                BattleCommandManager.Instance.SelectedResidents.Remove(this);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogError($"[实体] 入驻失败！{targetCarrier.GetCarrierName()} 可能反馈 TryAddStaff 为 false");
            BuildingBase reservedBuilding = targetCarrier as BuildingBase;
            targetCarrier = null;
            MyData.Status = ResidentStatus.Idle;
            MyData.CurrentCarrierID = string.Empty;
            if (reservedBuilding != null) reservedBuilding.NotifyStaffReservationChanged();
            if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();
        }
    }

    private void HandleMovement()
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 targetPos = currentPath[pathIndex];
        float dist = Vector2.Distance(transform.position, targetPos);

        if (dist < 0.2f)
        {
            pathIndex++;
        }
        else
        {
            Vector2 dir = (targetPos - transform.position).normalized;
            rb.velocity = dir * MoveSpeed;

            // 🌟 视觉平滑：根据移动方向水平翻转 Sprite
            if (Mathf.Abs(dir.x) > 0.01f)
            {
                float targetScaleX = dir.x > 0 ? 1f : -1f;
                Transform visual = transform.Find("Visual_Sprite");
                if (visual != null)
                    visual.localScale = new Vector3(targetScaleX, 1, 1);
            }
        }
    }
    // --- 选中状态控制 ---
    public void SetSelected(bool isSelected)
    {
        if (SelectionCircle != null) SelectionCircle.SetActive(isSelected);
    }

    public DamageReceiver GetReceiver() => GetComponent<DamageReceiver>();

    private void OnEnable()
    {
        if (!ActiveResidents.Contains(this)) ActiveResidents.Add(this);
        // 🌟 订阅死亡事件
        var dr = GetComponent<DamageReceiver>();
        if (dr != null) dr.OnEntityDeath += HandleDeath;
    }

    private void OnDisable()
    {
        LogisticsManager.Instance?.ReleaseResident(this);
        ActiveResidents.Remove(this);
        // 取消订阅，防止内存泄漏
        var dr = GetComponent<DamageReceiver>();
        if (dr != null) dr.OnEntityDeath -= HandleDeath;
    }

    public void SyncRuntimeStateToData()
    {
        if (MyData == null) return;
        DamageReceiver dr = GetComponent<DamageReceiver>();
        if (dr != null) MyData.CurrentHP = dr.CurrentHP;
    }

    private void HandleDeath()
    {
        // 1. 通知人口账本减员 (释放空间)
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.NotifyResidentDeath(this);
        }

        // 2. 从指挥官的选中列表中剔除自己
        if (BattleCommandManager.Instance != null)
        {
            BattleCommandManager.Instance.SelectedResidents.Remove(this);
        }

        // 3. 视觉表现：这里未来可以播一个倒地动画或爆炸特效
        Debug.Log($"<color=gray>【系统】</color> 居民实体 {gameObject.name} 已从物理世界移除。");

        // 4. 彻底销毁物体
        Destroy(gameObject);
    }
}
