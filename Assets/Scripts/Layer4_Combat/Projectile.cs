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

        // 撞击判定 (距离极近时视为命中)
        if (Vector3.Distance(transform.position, target.position) < 0.2f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        // 将伤害逻辑提取到一个公共静态方法，因为近战（Melee）也会用到完全一样的结算！
        WeaponDamageHandler.DeliverDamage(transform.position, target, damage, weaponData);

        // 子弹完成使命，销毁自己
        Destroy(gameObject);
    }
}