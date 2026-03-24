using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private float speed;
    public bool IsFiredByEnemy = false;

    public void Fire(Transform target, float damage, RuntimeWeapon data, bool isEnemyFire = false)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;
        this.IsFiredByEnemy = isEnemyFire;

        this.speed = data.GetStat(StatType.ProjectileSpeed);
        if (this.speed <= 0) this.speed = 10f;

        if (CombatSandbox.Instance != null)
        {
            this.speed *= CombatSandbox.Instance.SpeedMultiplier;
        }

        string shooter = isEnemyFire ? "敌人" : "玩家";
        Debug.Log($"<color=#FFFF00>【子弹发射】</color> {shooter} 开火！武器: {data.WeaponName}，目标: {(target ? target.name : "无")}");
    }

    private void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    private void HitTarget()
    {
        Debug.Log($"<color=#FFFF00>【子弹引爆】</color> {weaponData.WeaponName} 的子弹成功命中目标: {target.name}，正在派发 ECA 伤害总线...");

        ECAContext context = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = target,
            BaseDamage = damage,
            SourceWeapon = weaponData
        };

        if (weaponData.OnHitActions != null)
        {
            foreach (var action in weaponData.OnHitActions)
            {
                if (action != null) action.Execute(context);
            }
        }
        Destroy(gameObject);
    }

    private void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("Projectile");
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.isKinematic = true; }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { col = gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; }
        else col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();

        // 【极其硬核的调试播报】
        if (receiver != null)
        {
            bool isFriendlyFire = (receiver.isEnemy == this.IsFiredByEnemy);
            if (isFriendlyFire)
            {
                // 打到自己人了，或者穿过了自己
                // Debug.Log($"[子弹穿透] 穿过友军: {receiver.gameObject.name}"); 
            }
            else
            {
                // 打到敌人了！
                this.target = receiver.transform;
                HitTarget();
            }
        }
    }
}