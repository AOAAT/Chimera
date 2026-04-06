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

    // 👇【核心新增】：视觉穿透补偿！
    [Header("=== 视觉穿透与延迟销毁 ===")]
    [Tooltip("子弹命中后，是否允许它在视觉上继续往前飞一小段距离 (营造贯穿感)？")]
    public bool EnableVisualPenetration = false;

    [Tooltip("命中后，模型/拖尾继续存留的时间 (秒)。建议激光填 0.1~0.2")]
    public float PostHitLingerTime = 0f;

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
        // 👇 如果已经命中，且开启了穿透补偿，子弹不再追踪，而是顺着惯性继续飞！
        if (hasHit)
        {
            if (EnableVisualPenetration)
            {
                // 顺着最后的方向继续飞，营造穿透肉体的视觉效果
                transform.position += transform.right * speed * Time.deltaTime;
            }
            return;
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        // 正常巡航追踪
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // 👇【靶心修正】：现在不仅查距离，如果是激光这种极速武器，很容易一帧穿透！
        // 如果我们离目标的【物理中心】非常近了，强制引爆！
        Vector3 targetCenter = target.position;
        Collider2D col = target.GetComponentInChildren<Collider2D>();
        if (col != null) targetCenter = col.bounds.center;

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

        // 👇【视觉补偿核心】：命中后，不立刻销毁！
        // 如果配了延迟时间（比如激光），让它再飞一会儿，但关掉物理碰撞防止二次伤害！
        if (PostHitLingerTime > 0f)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false; // 关闭碰撞，它现在只是个幻影

            Destroy(gameObject, PostHitLingerTime); // 延迟销毁
        }
        else
        {
            Destroy(gameObject); // 普通子弹，立刻销毁
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