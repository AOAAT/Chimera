using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private RuntimeChimeraData ownerData;
    private Transform shooter;
    private float speed;

    private bool isEnemyFire;
    private bool isCritical;
    private bool hasHit = false;
    private bool hitAllies = false;

    [Header("=== 轨迹配置 ===")]
    public bool EnableHoming = true;
    public float TurnSpeed = 1500f;

    private GameObject myPrefabSource;
    private Vector2 currentDirection;
    private int generation = 0;
    private Vector3 lastPosition;
    private float lifeTimer;

    // 【新增】：射线检测的缓存数组，避免每帧分配内存
    private RaycastHit2D[] hitResults = new RaycastHit2D[5];
    private ECAContext capturedContext; // 记录发射时的上下文

    // --- Projectile.cs 局部修改 ---

    private float speedOverride = -1f;

    public void FireV2(ECAContext fireContext)
    {
        this.capturedContext = fireContext;
        this.target = fireContext.PrimaryTarget;
        this.shooter = fireContext.SourceEntity;
        this.weaponData = fireContext.SourceWeapon;
        this.isEnemyFire = fireContext.IsEnemyFire;

        // 👇 从 Context 中同步代际和过滤逻辑
        this.generation = fireContext.Generation;
        this.hitAllies = fireContext.HitAllies;

        this.currentDirection = transform.right;
        this.lastPosition = transform.position;

        float speedMult = CombatSandbox.GetSpeed(1f);
        this.speed = (speedOverride > 0 ? speedOverride : (weaponData != null ? weaponData.GetStat(StatType.ProjectileSpeed) : 10f)) * speedMult;

        this.hasHit = false;
        this.lifeTimer = 5f;
        gameObject.layer = LayerMask.NameToLayer("Projectile");
    }

    private void Update()
    {
        if (hasHit) return;
        if (target != null)
        {
            var targetReceiver = target.GetComponentInParent<DamageReceiver>();
            if (targetReceiver != null && targetReceiver.CurrentHP <= 0)
            {
                target = null; // 目标死亡，失去信号
            }
        }
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0) { DespawnMe(); return; }

        // --- 3. 处理追踪转向 ---
        if (EnableHoming && target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 targetCenter = target.position;
            Collider2D col = target.GetComponentInChildren<Collider2D>();
            if (col != null) targetCenter = col.bounds.center;

            Vector2 directionToTarget = (targetCenter - transform.position).normalized;
            float distToTarget = Vector2.Distance(transform.position, targetCenter);

            // 🌟 [核心修复]：角速度补偿逻辑
            // 如果距离小于 1.5 米，强行进入“必中模式”，瞬间转向目标
            if (distToTarget < 1.5f)
            {
                currentDirection = directionToTarget;
            }
            else
            {
                // 正常的平滑转向
                currentDirection = Vector3.RotateTowards(currentDirection, directionToTarget, TurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
            }

            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // 2. 计算本帧期望位移
        Vector3 nextPosition = transform.position + (Vector3)currentDirection * speed * Time.deltaTime;

        // --- 核心修复 2：全量路径扫描（解决穿隧问题） ---
        // 我们不再依赖 OnTriggerEnter，而是手动扫描从 lastPosition 到 nextPosition 的所有障碍物
        CheckPathCollision(lastPosition, nextPosition);

        if (!hasHit)
        {
            transform.position = nextPosition;
            lastPosition = transform.position;
        }
    }

    private void CheckPathCollision(Vector3 start, Vector3 end)
    {
        Vector2 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);

        // --- 🌟 [核心加固]：扩大扫描层级，确保覆盖 Body 和 Hitbox ---
        int targetLayer;
        if (isEnemyFire)
            // 敌方子弹扫描：玩家机甲 + 玩家居民
            targetLayer = LayerMask.GetMask("Player_Hitbox", "Player_Body", "Resident");
        else
            // 玩家子弹扫描：敌人机甲 + 敌人受击盒
            targetLayer = LayerMask.GetMask("Enemy_Hitbox", "Enemy_Body");
        // 如果是奶弹模式，反转层级
        if (hitAllies)
            targetLayer = isEnemyFire ? LayerMask.GetMask("Enemy_Hitbox", "Enemy_Body") : LayerMask.GetMask("Player_Hitbox", "Player_Body");

        int hits = Physics2D.RaycastNonAlloc(start, dir, hitResults, dist, targetLayer);

        for (int i = 0; i < hits; i++)
        {
            RaycastHit2D hit = hitResults[i];
            if (hit.collider == null) continue;

            DamageReceiver receiver = hit.collider.GetComponentInParent<DamageReceiver>();

            // 🌟 [核心加固]：只有目标活着，才判定为“击中”
            if (receiver != null && receiver.CurrentHP > 0)
            {
                bool isHittingShooter = (shooter != null) && (receiver.transform == shooter || receiver.transform.IsChildOf(shooter));

                if (!isHittingShooter)
                {
                    transform.position = hit.point;
                    HitTarget(receiver.transform);
                    return;
                }
            }
            // 如果 receiver 死了，循环会继续，子弹会从“尸体”上穿过去
        }


    }

    // 原有的 OnTriggerEnter2D 作为第二重保险（处理静止物体重叠）
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();
        if (receiver != null && shooter != null)
        {
            // 永远不打自己
            if (receiver.transform == shooter || receiver.transform.IsChildOf(shooter)) return;

            bool isTargetEnemy = receiver.isEnemy;

            // --- 👇【ECA 2.0 判定逻辑】---
            bool isValidHit = false;

            if (this.hitAllies) // 如果是奶弹
            {
                // 只有当目标的阵营 和 我方阵营 一致时，才判定为命中
                isValidHit = (this.isEnemyFire == isTargetEnemy);
            }
            else // 如果是普通子弹
            {
                // 只有当目标的阵营 和 我方阵营 相反时，才判定为命中
                isValidHit = (this.isEnemyFire != isTargetEnemy);
            }
            // ----------------------------

            if (isValidHit)
            {
                HitTarget(receiver.transform);
            }
        }
    }
    private void HitTarget(Transform actualHitTarget)
    {
        // 1. 防重复进入
        if (hasHit) return;
        hasHit = true;

        // 2. 物理熔断：停止移动，防止穿透感
        speed = 0;

        // 3. 逻辑分发
        if (weaponData != null && capturedContext != null)
        {
            // 将当前的命中位置同步到上下文
            capturedContext.ImpactPoint = transform.position;

            // 🌟 关键：调用武器的命中管线
            weaponData.TriggerHitPipeline(actualHitTarget, transform.position, capturedContext);
        }
        else
        {
            // 兜底：如果没有复杂的 ECA 逻辑，直接通过 DamageReceiver 扣血
            var dr = actualHitTarget.GetComponentInParent<DamageReceiver>();
            if (dr != null) dr.TakeDamage(damage, "未知来源");
        }

        // 4. 🌟 [核心加固]：强制消除
        // 无论是从对象池回收还是直接销毁，确保本帧结束前它不再存在
        DespawnMe();
    }
    private void DespawnMe()
    {
        if (myPrefabSource != null) SimplePool.Despawn(myPrefabSource, gameObject);
        else Destroy(gameObject);
    }

    public void SetSpeedOverride(float s) => speedOverride = s;

}