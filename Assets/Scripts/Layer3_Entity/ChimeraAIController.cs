using System.Linq;
using UnityEngine;
using static ComponentDataSO;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private Collider2D myCollider;

    [Header("=== 动态物理计算结果 ===")]
    public float CurrentSpeed;
    public float MaxStamina;
    public float CurrentStamina;
    public bool IsExhausted = false;
    private float exhaustionTimer = 0f;

    // 缓存武器射程数据
    private float maxWeaponRange = 0f;
    private float minWeaponRange = 0f;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;

        // 👇【核心修复】：获取沙盒全局度量衡
        float speedMult = 1f;
        float distMult = 1f;
        if (CombatSandbox.Instance != null)
        {
            speedMult = CombatSandbox.Instance.SpeedMultiplier;
            distMult = CombatSandbox.Instance.DistanceMultiplier;
        }

        // 1. 终极物理公式：速度 = 动力 / 质量 * 全局速度缩放
        float mass = Mathf.Max(runtimeData.TotalMass, 0.5f);
        CurrentSpeed = Mathf.Max(0.1f, (runtimeData.TotalEnginePower / mass) * speedMult);

        // 2. 终极耐力公式：内部消耗逻辑，不需要物理缩放
        float powerCost = Mathf.Max(runtimeData.TotalPowerCost, 1f);
        MaxStamina = Mathf.Max(20f, (runtimeData.TotalEnginePower / powerCost) * 0.1f);
        CurrentStamina = MaxStamina;

        // 3. 统计射程，并【极其关键地】乘以全局距离缩放！
        if (runtimeData.EquippedWeapons.Count > 0)
        {
            maxWeaponRange = runtimeData.EquippedWeapons.Max(w => w.GetStat(StatType.MaxRange)) * distMult;
            minWeaponRange = runtimeData.EquippedWeapons.Min(w => w.GetStat(StatType.MaxRange)) * distMult;
        }

        // 把躲避型的安全距离也同步缩放
        runtimeData.SafeDodgeDistance *= distMult;

        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
    }
    private void Update()
    {
        if (runtimeData == null) return;

        // 👇【核心静默控制】：如果没开战，引擎处于怠速状态，严禁挂挡！
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero; // 拉手刹
            return;
        }


        // 状态 1：过热瘫痪态 (被榨干耐力的惩罚)
        if (IsExhausted)
        {
            // 👇【核心修复】：物理手刹！瘫痪瞬间强行清空刚体的惯性速度！
            if (rb != null) rb.velocity = Vector2.zero;

            exhaustionTimer -= Time.deltaTime;
            CurrentStamina += (MaxStamina * 0.2f) * Time.deltaTime;

            // 呼叫全身染色系统，变成暗红警告色！
            TintMech(new Color(1f, 0.5f, 0.5f));

            if (exhaustionTimer <= 0)
            {
                IsExhausted = false;
                // 冷却完毕，全身恢复白色！
                TintMech(Color.white);
            }
            return; // 瘫痪时什么都做不了！
        }

        // 正常状态下，确保颜色是白的（防止某些奇怪的打断）
        // 👇【核心修复】
        TintMech(Color.white);

        FindTarget();
        HandleMovementAndStamina();
    }
    private void FindTarget()
    {
        var allEnemies = FindObjectsOfType<DamageReceiver>().Where(e => e.isEnemy && e.CurrentHP > 0).ToList();

        // 👇【新增雷达日志】：每秒打印一次，看看场上到底有几个活着的敌人？
        // (为了防止每帧打印卡死控制台，咱们粗略限制一下打印频率)
        if (Time.frameCount % 60 == 0)
        {
            //Debug.Log($"【雷达扫描】场上活着的、且被标记为Enemy的敌人数量: {allEnemies.Count}");
        }

        if (allEnemies.Count == 0)
        {
            currentTarget = null;
            return;
        }

        switch (runtimeData.TargetingLogic)
        {
            case TargetingStrategy.Nearest:
                currentTarget = allEnemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).First().transform;
                break;
            case TargetingStrategy.MaxHPHighest:
                currentTarget = allEnemies.OrderByDescending(e => e.MaxHP).First().transform;
                break;
            case TargetingStrategy.MaxHPLowest:
                currentTarget = allEnemies.OrderBy(e => e.MaxHP).First().transform;
                break;
            case TargetingStrategy.CurrentHPHighest:
                currentTarget = allEnemies.OrderByDescending(e => e.CurrentHP).First().transform;
                break;
            case TargetingStrategy.CurrentHPLowest:
                currentTarget = allEnemies.OrderBy(e => e.CurrentHP).First().transform;
                break;
        }
    }

    private void HandleMovementAndStamina()
    {
        if (currentTarget == null)
        {
            // 天下太平，原地待命
            if (rb != null) rb.velocity = Vector2.zero;
            if (CurrentStamina < MaxStamina) CurrentStamina += 3f * Time.deltaTime;
            return;
        }

        bool isMoving = false;

        // 👇【核心修复 1】：获取绝对的“逻辑心脏”世界坐标！
        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);

        // 获取基础方向（以心脏为基准指向敌人）
        Vector3 dirToTarget = (currentTarget.position - logicCenter).normalized;
        float dist = Vector3.Distance(logicCenter, currentTarget.position);

        // 👇【核心修复 2】：寻找敌人身上的受击判定框 (Hitbox)
        // 因为咱们做了层级分离，敌人的 Hitbox 可能在子节点上，优先找 isTrigger 的那个！
        Collider2D[] enemyCols = currentTarget.GetComponentsInChildren<Collider2D>();
        Collider2D targetCol = null;
        foreach (var c in enemyCols) { if (c.isTrigger) { targetCol = c; break; } }
        if (targetCol == null && enemyCols.Length > 0) targetCol = enemyCols[0]; // 兜底

        if (targetCol != null)
        {
            // 👇【完美闭环】：从“我的心脏”到“敌人最边缘”的距离！
            // 现在的 dist 计算结果，和 WeaponModule 里的计算结果绝对是 100% 一模一样的！
            Vector2 closestPoint = targetCol.ClosestPoint(logicCenter);
            dist = Vector2.Distance(logicCenter, closestPoint);
        }

        Vector2 targetVelocity = Vector2.zero;

        // 战术走位判断
        if (runtimeData.MovementLogic == MovementStrategy.Dodge && dist < runtimeData.SafeDodgeDistance)
        {
            targetVelocity = -dirToTarget * CurrentSpeed;
            isMoving = true;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
            isMoving = true;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Firepower && dist > minWeaponRange)
        {
            // 激进火力型：只要没进入最小盲区，就一直往前怼！
            targetVelocity = dirToTarget * CurrentSpeed;
            isMoving = true;
        }

        // 瘫痪时速度归零
        if (IsExhausted) targetVelocity = Vector2.zero;

        // 物理接管
        if (rb != null) rb.velocity = targetVelocity;

        // --- 耐力核心运转 ---
        if (isMoving)
        {
            CurrentStamina -= 5f * Time.deltaTime;
            if (CurrentStamina <= 0)
            {
                CurrentStamina = 0;
                IsExhausted = true;
                exhaustionTimer = 3f;
                Debug.LogWarning($"[{runtimeData.UnitName}] 引擎过热！强制瘫痪 3 秒！");
            }
        }
        else
        {
            if (CurrentStamina < MaxStamina) CurrentStamina += 3f * Time.deltaTime;
        }
    }
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && runtimeData != null)
        {
            if (runtimeData.MovementLogic == ComponentDataSO.MovementStrategy.Dodge)
            {
                Gizmos.color = Color.green;
                // 👇【圆心统一】：画安全距离圈时，也要以心脏为圆心！
                Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
                Gizmos.DrawWireSphere(logicCenter, runtimeData.SafeDodgeDistance);
            }
        }
    }

    private void TintMech(Color targetColor)
    {
        // 瞬间扫描机甲身上和所有子节点里的图层，全部统一上色！
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers)
        {
            sr.color = targetColor;
        }
    }
}