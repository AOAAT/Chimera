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

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0) { DespawnMe(); return; }

        // 1. 处理追踪转向
        if (EnableHoming && target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 targetCenter = target.position;
            Collider2D col = target.GetComponentInChildren<Collider2D>();
            if (col != null) targetCenter = col.bounds.center;

            Vector2 directionToTarget = (targetCenter - transform.position).normalized;
            currentDirection = Vector3.RotateTowards(currentDirection, directionToTarget, TurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;

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

        // 判定攻击层级
        int layerMask = isEnemyFire ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
        if (hitAllies) layerMask = isEnemyFire ? LayerMask.GetMask("Enemy_Hitbox") : LayerMask.GetMask("Player_Hitbox");

        // 使用 RaycastAll 扫描这段路径上所有的碰撞体
        int hits = Physics2D.RaycastNonAlloc(start, dir, hitResults, dist, layerMask);

        for (int i = 0; i < hits; i++)
        {
            RaycastHit2D hit = hitResults[i];
            if (hit.collider == null) continue;

            DamageReceiver receiver = hit.collider.GetComponentInParent<DamageReceiver>();

            // 👇【核心修复点】：增加 shooter != null 的判定
            if (receiver != null) 
            {
                // 如果射手已经不在了，或者目标不是射手本人且不是射手的子对象
                bool isSelf = (shooter != null) && (receiver.transform == shooter || receiver.transform.IsChildOf(shooter));

                if (!isSelf)
                {
                    this.target = receiver.transform;
                    transform.position = hit.point;
                    HitTarget(receiver.transform);
                    return;
                }
            }
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
        if (hasHit) return;

        // 🌟【核心修复】：如果撞到的是射手本人，或者是射手的受击盒（子物体），直接穿透，不判定为命中
        if (actualHitTarget == shooter || (shooter != null && actualHitTarget.IsChildOf(shooter)))
        {
            return;
        }

        hasHit = true;

        // 调试日志：确认真正命中了有效目标
        Debug.Log($"<color=cyan>【子弹-撞击】</color> 有效命中: {actualHitTarget.name} | 奶弹模式: {this.hitAllies}");

        if (weaponData != null && capturedContext != null)
        {
            // 将控制权交还给所属零件的 OnHit 管线
            weaponData.TriggerHitPipeline(actualHitTarget, transform.position, capturedContext);
        }
        else
        {
            Debug.LogWarning($"<color=orange>【子弹-警告】</color> 命中后无法分发管线，缺少上下文。");
        }

        DespawnMe();
    }
    private void DespawnMe()
    {
        if (myPrefabSource != null) SimplePool.Despawn(myPrefabSource, gameObject);
        else Destroy(gameObject);
    }

    public void SetSpeedOverride(float s) => speedOverride = s;

}