using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ResidentEntity : MonoBehaviour
{
    [Header("=== 绑定的数据 ===")]
    public ResidentData MyData;

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
        }

        SetSelected(false);
        transform.position += (Vector3)UnityEngine.Random.insideUnitCircle * 0.01f;
    }

    public void SetDestination(Vector2 worldPos) // 或者 SetManualMovePoint
    {
        // 🌟 核心：在计算新路径前，立即切断当前所有物理惯性
        if (rb != null) rb.velocity = Vector2.zero;

        currentPath = GridPathfinder.FindPath(transform.position, worldPos);
        pathIndex = 0;

        // 如果路径只有1个点（就在脚下），直接清理掉，防止原地抽搐
        if (currentPath != null && currentPath.Count <= 1) currentPath = null;
    }

    public void OrderGarrison(IResidentCarrier carrier)
    {
        targetCarrier = carrier;
        Vector3 gatePos = carrier.GetInteractionPoint();

        // 🔍 DEBUG 2: 确认门的位置
        Debug.Log($"[实体] {MyData.ResidentName} 收到入驻请求。门口世界坐标: {gatePos}");

        SetDestination(gatePos);
    }
    private void Update()
    {
        HandleMovement();

        if (targetCarrier != null)
        {
            float distToGate = Vector2.Distance(transform.position, targetCarrier.GetInteractionPoint());

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
            targetCarrier = null;
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
        // 🌟 订阅死亡事件
        var dr = GetComponent<DamageReceiver>();
        if (dr != null) dr.OnEntityDeath += HandleDeath;
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        var dr = GetComponent<DamageReceiver>();
        if (dr != null) dr.OnEntityDeath -= HandleDeath;
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