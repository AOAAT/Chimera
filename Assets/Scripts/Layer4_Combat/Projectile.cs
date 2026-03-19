using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform target;
    private float damage;
    private RuntimeWeapon weaponData;
    private float speed;

    // 弹药点火装填
    public void Fire(Transform target, float damage, RuntimeWeapon data)
    {
        this.target = target;
        this.damage = damage;
        this.weaponData = data;

        // 读取子弹基础速度
        this.speed = data.GetStat(StatType.ProjectileSpeed);
        if (this.speed <= 0) this.speed = 10f;

        // 👇【核心修复】：接入全局战区沙盒的速度比例尺！
        if (CombatSandbox.Instance != null)
        {
            this.speed *= CombatSandbox.Instance.SpeedMultiplier;
        }
    }

    private void Update()
    {
        // 如果目标死了或者丢了，子弹自毁（未来可以优化为飞向最后已知坐标）
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        // 飞向目标
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

private void HitTarget()
    {
        ECAContext context = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = target,
            BaseDamage = damage,
            SourceWeapon = weaponData
        };

        // 这里只负责呼叫积木，绝对不能自己扣血！
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
        // 自动给子弹披上物理外衣
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.isKinematic = true; }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { col = gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; }
        else col.isTrigger = true; // 确保一定是触发器
    }

    // 👇【极其硬核的物理命中感知】
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 顺藤摸瓜：撞到了左手或底盘？直接找它最上层的血条！
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();

        // 如果碰到了带血条的物体，且它是敌人！
        if (receiver != null && receiver.isEnemy)
        {
            // 把目标强行扭转为当前撞到的这个倒霉蛋，然后结算伤害！
            this.target = receiver.transform;
            HitTarget();
        }
    }
}