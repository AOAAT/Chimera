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

    [Header("=== 视觉表现 ===")]
    public bool EnableVisualPenetration = false;
    public float PostHitLingerTime = 0f;

    [Header("=== 弹道轨迹 ===")]
    public bool EnableHoming = true;
    public float TurnSpeed = 1500f;

    private Vector2 currentDirection;
    private int generation = 0;
    private Vector3 lastPosition;

    public void Fire(Transform target, float damage, RuntimeWeapon data, RuntimeChimeraData owner, Transform shooter, bool isEnemy, bool isCrit, int gen, bool targetAllies)
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

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        this.speed = data != null ? data.GetStat(StatType.ProjectileSpeed) : 10f;
        this.speed *= speedMult;

        if (this.speed <= 0.01f)
        {
            Debug.LogError($"<color=red>【子弹故障】</color> 来源武器：{data.WeaponName} | 原始速度：{speed} | 缩放后速度：{this.speed}。速度太低导致停滞！");
        }

        gameObject.layer = LayerMask.NameToLayer("Projectile");
        currentDirection = transform.right;

        if (currentDirection.sqrMagnitude < 0.01f)
        {
            Debug.LogError($"<color=red>【子弹故障】</color> 方向向量丢失！请检查武器插槽的旋转角度。");
        }
        lastPosition = transform.position;
    }

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.isKinematic = true; }
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { col = gameObject.AddComponent<CircleCollider2D>(); col.isTrigger = true; }
        Destroy(gameObject, 10f);
    }

    private void Update()
    {
        if (hasHit)
        {
            if (EnableVisualPenetration) transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
            return;
        }

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

        transform.position += (Vector3)currentDirection * speed * Time.deltaTime;
        CheckCollisionPhysics();
        lastPosition = transform.position;
    }

    private void CheckCollisionPhysics()
    {
        float displacement = Vector3.Distance(lastPosition, transform.position);
        Vector3 dir = (transform.position - lastPosition).normalized;
        int layerMask = isEnemyFire ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
        if (hitAllies) layerMask = isEnemyFire ? LayerMask.GetMask("Enemy_Hitbox") : LayerMask.GetMask("Player_Hitbox");

        RaycastHit2D hit = Physics2D.Raycast(lastPosition, dir, displacement, layerMask);
        if (hit.collider != null)
        {
            DamageReceiver receiver = hit.collider.GetComponentInParent<DamageReceiver>();
            if (receiver != null && receiver.transform != shooter)
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
        DamageReceiver receiver = collision.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            bool isTargetEnemy = receiver.isEnemy;
            bool isValidHit = (isEnemyFire != isTargetEnemy);
            if (hitAllies) isValidHit = (isEnemyFire == isTargetEnemy && receiver.transform != shooter);
            if (receiver == null) return;
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

        ECAContext context = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = target, BaseDamage = damage, SourceWeapon = weaponData, ChassisData = ownerData, IsEnemyFire = this.isEnemyFire, IsCriticalHit = this.isCritical };
        context.CustomStates["Gen"] = generation;

        if (weaponData != null && weaponData.OnHitActions != null)
        {
            foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(context);
        }
        if (ownerData != null && ownerData.GlobalOnHitActions != null)
        {
            foreach (var action in ownerData.GlobalOnHitActions) if (action != null) action.Execute(context);
        }

        if (PostHitLingerTime > 0f)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            Destroy(gameObject, PostHitLingerTime);
        }
        else Destroy(gameObject);
    }
}