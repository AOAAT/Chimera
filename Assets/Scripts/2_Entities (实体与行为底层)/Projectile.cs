using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private float speed;

    private bool isEnemyFire;
    private bool isCritical;
    private bool hasHit = false;
    private bool hitAllies = false;

    [Header("=== 视觉表现 ===")]
    public bool EnableVisualPenetration = false;
    public float PostHitLingerTime = 0f;

    [Header("=== 弹道轨迹控制 ===")]
    [Tooltip("是否开启追踪？(霰弹枪请在预制体或ECA中设为 false)")]
    public bool EnableHoming = true;

    [Tooltip("每秒最大转向角度 (仅在 EnableHoming 开启时有效)")]
    public float TurnSpeed = 1500f;

    // 子弹当前的物理飞行方向
    private Vector2 currentDirection;

    // ==========================================
    // 发射接口
    // ==========================================
    public void Fire(Transform target, float damage, RuntimeWeapon data, bool isEnemy, bool isCrit, bool targetAllies = false)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.isEnemyFire = isEnemy;
        this.isCritical = isCrit;
        this.hitAllies = targetAllies;

        // 【度量衡修复】：应用全局速度缩放
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        this.speed = data != null ? data.GetStat(StatType.ProjectileSpeed) : 10f;
        if (this.speed <= 0) this.speed = 10f;
        this.speed *= speedMult;

        gameObject.layer = LayerMask.NameToLayer("Projectile");

        // 初始方向锁定为枪口当前的朝向 (transform.right)
        currentDirection = transform.right;

        // 安全销毁：防止子弹飞出世界后永不消失 (5秒射程保底)
        Destroy(gameObject, 5f);
    }

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.isKinematic = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (hasHit)
        {
            if (EnableVisualPenetration)
            {
                // 穿透模式下，继续顺着最后的方向飞
                transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
            }
            return;
        }

        // --- 弹道飞行逻辑 ---

        // 情况 A：开启了追踪且目标存活
        if (EnableHoming && target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 targetCenter = GetTargetCenter();
            Vector2 directionToTarget = (targetCenter - transform.position).normalized;

            // 平滑转向计算
            currentDirection = Vector3.RotateTowards(
                currentDirection,
                directionToTarget,
                TurnSpeed * Mathf.Deg2Rad * Time.deltaTime,
                0f
            ).normalized;

            // 同步图片旋转
            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        // 情况 B：直线飞行 (霰弹枪模式，或目标丢失)
        else
        {
            // 保持发射瞬间的 currentDirection 匀速直线运动
            // 无需旋转，因为发射时已经设置好了旋转角度
        }

        // 应用位移
        transform.position += (Vector3)currentDirection * speed * Time.deltaTime;

        // --- 命中判定逻辑 ---

        // 如果有明确的目标，执行距离辅助判定（防穿模）
        if (target != null && target.gameObject.activeInHierarchy)
        {
            // 【度量衡修复】：判定半径乘以全局距离缩放
            float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
            float hitThreshold = 0.2f * distMult;

            if (Vector3.Distance(transform.position, GetTargetCenter()) <= hitThreshold)
            {
                HitTarget();
            }
        }
    }

    private Vector3 GetTargetCenter()
    {
        if (target == null) return transform.position;
        Collider2D col = target.GetComponentInChildren<Collider2D>();
        return col != null ? col.bounds.center : target.position;
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
            IsEnemyFire = this.isEnemyFire,
            IsCriticalHit = this.isCritical
        };

        if (weaponData != null && weaponData.OnHitActions != null)
        {
            foreach (var action in weaponData.OnHitActions)
            {
                if (action != null) action.Execute(context);
            }
        }

        if (PostHitLingerTime > 0f)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Destroy(gameObject, PostHitLingerTime);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 在 Projectile.cs 内部
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        // 找到对方身上的血条组件
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();

        if (receiver != null)
        {
            // 1. 阵营判定：如果你是玩家射出的子弹，不能打玩家自己（除非是奶弹）
            bool isFriendly = (receiver.isEnemy == this.isEnemyFire);

            // 如果是子弹打敌人，或者奶弹打队友
            if (isFriendly == hitAllies)
            {
                // 👇【核心】：一旦撞击，立刻原地执行命中逻辑！
                this.target = receiver.transform;
                HitTarget();
            }
        }
    }
}