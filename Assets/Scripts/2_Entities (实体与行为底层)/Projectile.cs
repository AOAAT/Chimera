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

    public void Fire(Transform target, float damage, RuntimeWeapon data, RuntimeChimeraData owner, Transform shooter, bool isEnemy, bool isCrit, int gen, bool targetAllies, GameObject sourcePrefab)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.ownerData = owner;
        this.shooter = shooter;
        this.isEnemyFire = isEnemy;
        this.isCritical = isCrit;
        this.generation = gen;
        this.hitAllies = targetAllies;
        this.myPrefabSource = sourcePrefab;

        float speedMult = CombatSandbox.GetSpeed(1f);
        this.speed = (data != null ? data.GetStat(StatType.ProjectileSpeed) : 10f) * speedMult;

        this.hasHit = false;
        this.lifeTimer = 5f;
        currentDirection = transform.right;

        // --- 核心修复 1：初始化位置时，立即记录当前位置 ---
        lastPosition = transform.position;
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
                    HitTarget();
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
            if (receiver.transform == shooter || receiver.transform.IsChildOf(shooter)) return;

            bool isTargetEnemy = receiver.isEnemy;
            bool isValidHit = hitAllies ? (isEnemyFire == isTargetEnemy) : (isEnemyFire != isTargetEnemy);

            if (isValidHit)
            {
                this.target = receiver.transform;
                HitTarget();
            }
        }
    }

    private void HitTarget()
    {
        if (hasHit) return;
        hasHit = true;

        ECAContext context = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = target,
            BaseDamage = damage,
            SourceWeapon = weaponData,
            ChassisData = ownerData,
            IsEnemyFire = this.isEnemyFire,
            IsCriticalHit = this.isCritical,
            SourceEntity = shooter
        };
        context.CustomStates["Gen"] = generation;

        if (weaponData?.OnHitActions != null)
        {
            foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(context);
        }
        if (ownerData?.GlobalOnHitActions != null)
        {
            foreach (var action in ownerData.GlobalOnHitActions) if (action != null) action.Execute(context);
        }

        DespawnMe();
    }

    private void DespawnMe()
    {
        if (myPrefabSource != null) SimplePool.Despawn(myPrefabSource, gameObject);
        else Destroy(gameObject);
    }
}