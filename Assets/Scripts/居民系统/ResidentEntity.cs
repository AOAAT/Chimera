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
            // 1. 获取目标交互点（建筑的门口或机甲的中心）
            Vector3 targetPos = targetCarrier.GetInteractionPoint();
            float distToTarget = Vector2.Distance(transform.position, targetPos);

            // 2. 🌟 核心改进：根据载体类型设置动态判定半径
            // 如果载体是机甲，半径给 0.8m；如果是建筑（有门），给 0.4m 即可。
            float triggerRadius = (targetCarrier is MechUnit2D) ? 0.85f : 0.4f;

            // 3. 动态追逐（如果是移动中的机甲，每 10 帧更新一次路径）
            if (targetCarrier is MonoBehaviour mb && Time.frameCount % 10 == 0)
            {
                SetDestination(mb.transform.position);
            }

            // 4. 判定入场
            if (distToTarget < triggerRadius)
            {
                Debug.Log($"<color=yellow>[入驻成功]</color> {MyData.ResidentName} 已成功接触到 {targetCarrier.GetCarrierName()}，判定半径: {triggerRadius}");
                ExecuteEnterGarrison();
            }
        }
    }

    private void ExecuteEnterGarrison()
    {
        bool success = targetCarrier.TryAddStaff(this.MyData);

        if (success)
        {
            Debug.Log($"<color=green>[成功]</color> {MyData.ResidentName} 已登记并消失。");
            if (BattleCommandManager.Instance != null)
                BattleCommandManager.Instance.SelectedResidents.Remove(this);

            Destroy(gameObject);
        }
        else
        {
            // 诊断：为什么 TryAddStaff 拒绝了入驻？
            Debug.LogError($"<color=red>[失败]</color> {targetCarrier.GetCarrierName()} 拒绝了 {MyData.ResidentName} 的入驻请求。");
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