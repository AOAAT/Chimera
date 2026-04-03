using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private float speed;

    private bool isEnemyFire;
    private bool isCritical; // 👇【核心修复】：子弹现在会记住自己是不是暴击！
    private bool hasHit = false; // 👇【新增】：防连击锁，保证一颗子弹绝对只炸一次！

    public void Fire(Transform target, float damage, RuntimeWeapon data, bool isEnemy, bool isCrit)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.isEnemyFire = isEnemy;
        this.isCritical = isCrit; // 保存暴击状态

        this.speed = data.GetStat(StatType.ProjectileSpeed);
        if (this.speed <= 0) this.speed = 10f;
        if (CombatSandbox.Instance != null) this.speed *= CombatSandbox.Instance.SpeedMultiplier;

        // 👇【核心修复 1：绝对图层控制】：发车瞬间，强行注入 Projectile 图层，撕碎预制体的错误配置！
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
        // 如果已经炸了，或者目标蒸发了，直接销毁
        if (hasHit) return;
        if (target == null || !target.gameObject.activeInHierarchy) { Destroy(gameObject); return; }

        // 巡航追踪
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // 👇【核心修复 2：主程的神级兜底】：管你物理引擎碰没碰到，只要贴脸（距离 < 0.2f），强制执行引爆！
        if (Vector3.Distance(transform.position, target.position) <= 0.2f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        if (hasHit) return; // 防连击
        hasHit = true;      // 上锁！

        ECAContext context = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = target,
            BaseDamage = damage,
            SourceWeapon = weaponData,
            IsEnemyFire = this.isEnemyFire, // 传递阵营
            IsCriticalHit = this.isCritical // 👇【核心修复】：把暴击状态交还给 ECA 总线！
        };

        if (weaponData != null && weaponData.OnHitActions != null)
        {
            foreach (var action in weaponData.OnHitActions)
            {
                if (action != null) action.Execute(context);
            }
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();

        // 阵营比对：必须是异一阵营，才能引爆！
        if (receiver != null && receiver.isEnemy != this.isEnemyFire)
        {
            this.target = receiver.transform;
            HitTarget();
        }
    }
}