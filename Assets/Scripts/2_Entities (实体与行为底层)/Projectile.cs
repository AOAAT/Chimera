// --- 替换 Projectile.cs 全量代码 ---
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

    // 【优化】：用于归还对象池的来源引用
    private GameObject myPrefabSource;
    private Vector2 currentDirection;
    private int generation = 0;
    private Vector3 lastPosition;
    private float lifeTimer;

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
        this.myPrefabSource = sourcePrefab; // 记录来源

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        this.speed = (data != null ? data.GetStat(StatType.ProjectileSpeed) : 10f) * speedMult;

        this.hasHit = false;
        this.lifeTimer = 5f; // 5秒强制回收，防止飞出世界
        currentDirection = transform.right;
        lastPosition = transform.position;

        gameObject.layer = LayerMask.NameToLayer("Projectile");
    }

    // --- Projectile.cs 优化追踪代码 ---
    private void Update()
    {
        if (hasHit) return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0) { DespawnMe(); return; }

        if (EnableHoming && target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 targetPos = target.position;
            Collider2D col = target.GetComponentInChildren<Collider2D>();
            if (col != null) targetPos = col.bounds.center;

            float dist = Vector2.Distance(transform.position, targetPos);

            // --- 👇【核心修复：距离引信】---
            // 如果距离目标中心非常近（例如 0.4 个单位），直接判定为命中
            // 这样可以解决子弹“贴身不爆炸”和“绕圈不命中”的所有弹道问题
            if (dist < 0.4f)
            {
                HitTarget();
                return;
            }

            // 原有的转向逻辑保持...
            Vector2 directionToTarget = (targetPos - transform.position).normalized;
            currentDirection = Vector3.RotateTowards(currentDirection, directionToTarget, TurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;

            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        transform.position += (Vector3)currentDirection * speed * Time.deltaTime;

        // 原有的物理检测（作为中远距离的补充）
        CheckCollisionPhysics();
        lastPosition = transform.position;
    }

    private void CheckCollisionPhysics()
    {
        // 只有位移足够大才检测，防止慢速弹每帧浪费计算
        float displacement = Vector3.Distance(lastPosition, transform.position);
        if (displacement < 0.1f) return;

        Vector3 dir = (transform.position - lastPosition).normalized;
        int layerMask = isEnemyFire ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
        if (hitAllies) layerMask = isEnemyFire ? LayerMask.GetMask("Enemy_Hitbox") : LayerMask.GetMask("Player_Hitbox");

        RaycastHit2D hit = Physics2D.Raycast(lastPosition, dir, displacement, layerMask);
        if (hit.collider != null)
        {
            DamageReceiver receiver = hit.collider.GetComponentInParent<DamageReceiver>();
            // 排除射手本身及子物体
            if (receiver != null && receiver.transform != shooter && !receiver.transform.IsChildOf(shooter))
            {
                this.target = receiver.transform;
                transform.position = hit.point;
                HitTarget();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        // 排除射手本身
        if (shooter != null && (collision.transform == shooter || collision.transform.IsChildOf(shooter))) return;

        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
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