// --- START OF FILE Projectile.cs ---
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

    [Header("=== 视觉穿透与延迟销毁 ===")]
    public bool EnableVisualPenetration = false;
    public float PostHitLingerTime = 0f;

    // 👇【核心新增】：平滑制导系统！
    [Header("=== 弹道轨迹控制 ===")]
    [Tooltip("是否开启平滑追踪？(如果不勾，就是生硬直线；勾了就是追踪导弹/平滑光束)")]
    public bool EnableHoming = true;

    [Tooltip("每秒最大转向角度 (决定了光束/导弹拐弯的圆润程度。推荐：光束2000，导弹300)")]
    public float TurnSpeed = 1500f;

    // 记录子弹当前的飞行方向 (单位向量)
    private Vector2 currentDirection;

    public void Fire(Transform target, float damage, RuntimeWeapon data, bool isEnemy, bool isCrit)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.isEnemyFire = isEnemy;
        this.isCritical = isCrit;

        this.speed = data.GetStat(StatType.ProjectileSpeed);
        if (this.speed <= 0) this.speed = 10f;
        if (CombatSandbox.Instance != null) this.speed *= CombatSandbox.Instance.SpeedMultiplier;

        gameObject.layer = LayerMask.NameToLayer("Projectile");

        // 👇 记录初始发射方向 (就是生成时枪口的朝向)
        currentDirection = transform.right;
    }

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.isKinematic = true; }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { col = gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; }
        else col.isTrigger = true;
    }

    private void Update()
    {
        if (hasHit)
        {
            if (EnableVisualPenetration)
            {
                // 穿透时，顺着最后的惯性方向继续飞
                transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
            }
            return;
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetCenter = target.position;
        Collider2D col = target.GetComponentInChildren<Collider2D>();
        if (col != null) targetCenter = col.bounds.center;

        // ==========================================
        // 🧠 核心修复：弹道飞行逻辑重构
        // ==========================================

        if (EnableHoming)
        {
            // 1. 算出指向敌人的理想向量
            Vector2 directionToTarget = (targetCenter - transform.position).normalized;

            // 2. 算出当前方向和理想方向的角度差
            float rotateAmount = Vector3.Cross(currentDirection, directionToTarget).z;

            // 3. 限制最大转向角速度，让它“平滑”地扭过去！
            // 这里用了一个小技巧：RotateTowards 能完美控制最大旋转角度
            currentDirection = Vector3.RotateTowards(
                currentDirection,
                directionToTarget,
                TurnSpeed * Mathf.Deg2Rad * Time.deltaTime,
                0f
            ).normalized;

            // 4. 更新子弹自身的旋转角度 (让贴图或长拖尾始终对准飞行方向)
            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // 5. 按照最终平滑过的方向向前飞行！
            transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
        }
        else
        {
            // 如果不开启制导，就沿直线死板地飞 (适合普通机枪子弹)
            currentDirection = (targetCenter - transform.position).normalized;
            transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
        }

        // 碰撞判定保持不变
        if (Vector3.Distance(transform.position, targetCenter) <= 0.2f)
        {
            HitTarget();
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();
        if (receiver != null && receiver.isEnemy != this.isEnemyFire)
        {
            this.target = receiver.transform;
            HitTarget();
        }
    }
}