using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private float speed;

    // 👇【新增阵营标识】
    private bool isEnemyFire;

    // 👇【修复】：发车时传入阵营标识
    public void Fire(Transform target, float damage, RuntimeWeapon data, bool isEnemy = false)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.isEnemyFire = isEnemy;

        this.speed = data.GetStat(StatType.ProjectileSpeed);
        if (this.speed <= 0) this.speed = 10f;
        if (CombatSandbox.Instance != null) this.speed *= CombatSandbox.Instance.SpeedMultiplier;
    }

    private void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    private void HitTarget()
    {
        ECAContext context = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = target,
            BaseDamage = damage,
            SourceWeapon = weaponData,
            IsEnemyFire = this.isEnemyFire // 👇 把阵营传给爆炸积木！
        };

        if (weaponData.OnHitActions != null)
            foreach (var action in weaponData.OnHitActions)
                if (action != null) action.Execute(context);

        Destroy(gameObject);
    }

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.isKinematic = true; }
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { col = gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; }
        else col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();

        // 👇【核心修复】：必须是异一阵营，才能引爆！(机甲打敌人，或敌人打机甲)
        if (receiver != null && receiver.isEnemy != this.isEnemyFire)
        {
            this.target = receiver.transform;
            HitTarget();
        }
    }
}