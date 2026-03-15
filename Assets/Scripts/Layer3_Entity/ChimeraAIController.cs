using System.Linq;
using UnityEngine;
using static ComponentDataSO;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;

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
    }
    private void Update()
    {
        if (runtimeData == null) return;

        // 状态 1：过热瘫痪态 (被榨干耐力的惩罚)
        if (IsExhausted)
        {
            exhaustionTimer -= Time.deltaTime;
            CurrentStamina += (MaxStamina * 0.2f) * Time.deltaTime;

            // 👇【加上这两句】：过热时变成警告的暗红色！
            GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.5f);

            if (exhaustionTimer <= 0)
            {
                IsExhausted = false;
                // 👇【恢复原色】：冷却完毕，重新出发！
                GetComponent<SpriteRenderer>().color = Color.white;
            }
            return; // 瘫痪时什么都做不了！
        }

        // 正常状态下，确保颜色是白的（防止某些奇怪的打断）
        GetComponent<SpriteRenderer>().color = Color.white;

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
        // 👇【关键修改】：把判断找不到目标的逻辑单独拎出来！
        if (currentTarget == null)
        {
            // 如果你看到机甲没动，且控制台也没疯狂刷这句话，说明连 Update 都没进！
            // 如果疯狂刷这句话，说明确实是找不到小怪！
            return;
        }

        bool isMoving = false;
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;

        // 👇【日志移到这里】：只要有目标，就疯狂汇报！
        //Debug.Log($"【AI监控】已锁定: {currentTarget.name} | 距离: {dist} | 策略: {runtimeData.MovementLogic} | 耐力: {CurrentStamina}");
        //Debug.LogWarning($"【机甲听诊器】当前策略: {runtimeData.MovementLogic} | 最短射程: {minWeaponRange} | 最长射程: {maxWeaponRange} | 距敌: {dist}");
        if (runtimeData.MovementLogic == MovementStrategy.Dodge)
        {
            if (dist < runtimeData.SafeDodgeDistance)
            {
                transform.position -= dirToTarget * CurrentSpeed * Time.deltaTime;
                isMoving = true;
            }
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Survival)
        {
            if (dist > maxWeaponRange)
            {
                transform.position += dirToTarget * CurrentSpeed * Time.deltaTime;
                isMoving = true;
            }
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Firepower)
        {
            if (dist > minWeaponRange)
            {
                transform.position += dirToTarget * CurrentSpeed * Time.deltaTime;
                isMoving = true;
            }
        }

        // --- 耐力核心运转 ---
        // ... (保持你原来的耐力运转代码不变)
        // --- 耐力核心运转 ---
        if (isMoving)
        {
            // 移动时扣除耐力
            CurrentStamina -= 5f * Time.deltaTime; // 每秒扣5点，数值可配
            if (CurrentStamina <= 0)
            {
                CurrentStamina = 0;
                IsExhausted = true;
                exhaustionTimer = 3f; // 强制原地瘫痪 3 秒！
                Debug.LogWarning($"[{runtimeData.UnitName}] 引擎过热！强制瘫痪 3 秒！");
            }
        }
        else
        {
            // 停下时恢复耐力
            if (CurrentStamina < MaxStamina)
            {
                CurrentStamina += 3f * Time.deltaTime;
            }
        }
    }
    private void OnDrawGizmos()
    {
        // 只有当游戏运行中，且大脑已经通电拿到了数据才画
        if (Application.isPlaying && runtimeData != null)
        {
            // 如果当前的战术是“躲避型”
            if (runtimeData.MovementLogic == ComponentDataSO.MovementStrategy.Dodge)
            {
                // 设置画笔颜色为绿色
                Gizmos.color = Color.green;

                // 画出一个圆圈！(SafeDodgeDistance 在通电时已经乘过全局比例尺了，这里直接用就是绝对准确的物理距离)
                Gizmos.DrawWireSphere(transform.position, runtimeData.SafeDodgeDistance);
            }
        }
    }
}