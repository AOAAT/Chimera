using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeEnemySkill
{
    public EnemySkillSO SkillData;
    public float CurrentCooldown;
    public RuntimeWeapon DummyWeapon;
}

[RequireComponent(typeof(DamageReceiver))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    public EnemyDataSO MyData;

    private DamageReceiver myReceiver;
    private Rigidbody2D rb;

    private Transform currentTarget;
    private Collider2D targetCollider;
    private Collider2D myHitboxCollider;

    private List<RuntimeEnemySkill> runtimeSkills = new List<RuntimeEnemySkill>();
    private RuntimeEnemySkill currentIntent = null;

    // 👇【修复：加入了 Staggered 状态！】
    private enum MoveExecutionState { Ready, Charging, Dashing, Cooldown, Staggered }
    private MoveExecutionState currentMoveState = MoveExecutionState.Ready;
    private float moveStateTimer = 0f;
    private Vector2 lockedMoveDirection;

    private float lastFrameHP;
    private bool isDead = false;

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }

        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();

        // 1. 从图纸中读取最大生命值，兜底 100 血
        float maxHP = MyData.GetStat(StatType.HP);
        if (maxHP <= 0) maxHP = 100f;
        myReceiver.Initialize(maxHP, 0f);
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;

        // 👇👇👇【终极物理塑形：全自动双碰撞箱与图层分配】👇👇👇

        // 核心参数：获取怪物的贴图尺寸和缩放
        SpriteRenderer mainSr = GetComponentInChildren<SpriteRenderer>();
        Vector2 spriteSize = mainSr != null && mainSr.sprite != null ?
                             mainSr.sprite.bounds.size * MyData.VisualScaleMultiplier :
                             new Vector2(1f, 1f);

        // --- 物理肉体 (脚底板) ---
        // 1. 设置物理图层 (请在 Unity 里建一个叫 Enemy_Body 的 Layer)
        int bodyLayer = LayerMask.NameToLayer("Enemy_Body");
        if (bodyLayer != -1) gameObject.layer = bodyLayer;

        // 2. 刚体配置
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 3. 脚底板非触发器碰撞箱 (只占下半部分，用于阻挡和防穿模)
        BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
        physicsCol.isTrigger = false;
        physicsCol.size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.3f);
        physicsCol.offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.size.y / 2f));


        // --- 弱点 Hitbox (受击框) ---
        // 4. 寻找或创建 Hitbox 子物体
        Transform hitboxTrans = transform.Find("Enemy_Hitbox");
        GameObject hitboxObj = hitboxTrans != null ? hitboxTrans.gameObject : new GameObject("Enemy_Hitbox");
        if (hitboxTrans == null) hitboxObj.transform.SetParent(this.transform, false);

        // 5. 设置 Hitbox 图层 (请在 Unity 里建一个叫 Enemy_Hitbox 的 Layer)
        int hitboxLayer = LayerMask.NameToLayer("Enemy_Hitbox");
        if (hitboxLayer != -1) hitboxObj.layer = hitboxLayer;

        // 6. Hitbox 触发器碰撞箱 (1:1 包裹全身，用于接子弹和索敌)
        BoxCollider2D hitboxCol = hitboxObj.GetComponent<BoxCollider2D>();
        if (hitboxCol == null) hitboxCol = hitboxObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = spriteSize;
        hitboxCol.offset = Vector2.zero;

        // 7. 缓存给防追尾雷达使用
        myHitboxCollider = hitboxCol;
        // 👆👆👆==========================================👆👆👆


        // 解析怪物技能 (保持原样)
        foreach (var skillSO in MyData.Skills)
        {
            if (skillSO == null) continue;
            var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f };

            rSkill.DummyWeapon = new RuntimeWeapon
            {
                WeaponName = skillSO.SkillName,
                DeliveryType = skillSO.DeliveryType,
                ProjectilePrefab = skillSO.ProjectilePrefab,
                SourceSO = null
            };
            rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed;
            rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed;
            rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions);

            runtimeSkills.Add(rSkill);
        }

        ExecuteECAActions(MyData.OnSpawnActions, this.transform, null);
    }

    private void Update()
    {
        if (isDead) return;

        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, this.transform, null);
            lastFrameHP = myReceiver.CurrentHP;
        }

        if (myReceiver.CurrentHP <= 0)
        {
            isDead = true;
            rb.velocity = Vector2.zero;
            ExecuteECAActions(MyData.OnDeathActions, this.transform, null);
            return;
        }

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) skill.CurrentCooldown -= Time.deltaTime;
        }

        FindTarget();
        HandleMovementAndCombat();
    }

    private void FindTarget()
    {
        var allPlayers = FindObjectsOfType<DamageReceiver>().Where(r => !r.isEnemy && r.CurrentHP > 0).ToList();
        if (allPlayers.Count == 0)
        {
            currentTarget = null;
            targetCollider = null;
            return;
        }

        switch (MyData.TargetingLogic)
        {
            case TargetingStrategy.MaxHPHighest: currentTarget = allPlayers.OrderByDescending(p => p.MaxHP).First().transform; break;
            case TargetingStrategy.MaxHPLowest: currentTarget = allPlayers.OrderBy(p => p.MaxHP).First().transform; break;
            case TargetingStrategy.CurrentHPHighest: currentTarget = allPlayers.OrderByDescending(p => p.CurrentHP).First().transform; break;
            case TargetingStrategy.CurrentHPLowest: currentTarget = allPlayers.OrderBy(p => p.CurrentHP).First().transform; break;
            case TargetingStrategy.Nearest:
            default:
                currentTarget = allPlayers.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).First().transform;
                break;
        }

        if (currentTarget != null)
        {
            Collider2D[] targetCols = currentTarget.GetComponentsInChildren<Collider2D>();
            foreach (var c in targetCols)
            {
                if (c.isTrigger) { targetCollider = c; break; }
            }
            if (targetCollider == null) targetCollider = currentTarget.GetComponentInChildren<Collider2D>();
        }
    }

    private void HandleMovementAndCombat()
    {
        if (currentTarget == null || MyData.MoveType == EnemyMoveType.Stationary)
        {
            ExecutePhysicalMovement(Vector2.zero, 1f);
            return;
        }

        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        float dist = 0f;
        Vector2 dirToTarget = Vector2.zero;

        if (targetCollider != null && myHitboxCollider != null)
        {
            ColliderDistance2D colDist = Physics2D.Distance(myHitboxCollider, targetCollider);
            dist = Mathf.Max(0f, colDist.distance);
            dirToTarget = (targetCollider.bounds.center - myHitboxCollider.bounds.center).normalized;
        }
        else
        {
            dist = Vector3.Distance(transform.position, currentTarget.position);
            dirToTarget = (currentTarget.position - transform.position).normalized;
        }

        float currentSpeed = MyData.GetStat(StatType.MoveSpeed) * speedMult;
        Vector2 targetVelocity = Vector2.zero;

        if (MyData.MovementLogic == EnemyMovementStrategy.Swarm)
        {
            targetVelocity = dirToTarget * currentSpeed;
            TryRollAndFireSkills(dist, dirToTarget, distMult);
        }
        else if (MyData.MovementLogic == EnemyMovementStrategy.Artillery)
        {
            float hoverDist = MyData.HoverDistance * distMult;
            if (dist > hoverDist + 0.5f) targetVelocity = dirToTarget * currentSpeed;
            else if (dist < hoverDist - 0.5f) targetVelocity = -dirToTarget * currentSpeed;
            else targetVelocity = Vector2.zero;

            TryRollAndFireSkills(dist, dirToTarget, distMult);
        }
        else if (MyData.MovementLogic == EnemyMovementStrategy.IntentDriven)
        {
            if (currentIntent == null)
            {
                var readySkills = runtimeSkills.Where(s => s.CurrentCooldown <= 0).ToList();
                if (readySkills.Count > 0)
                {
                    float totalWeight = readySkills.Sum(s => s.SkillData.SelectionWeight);
                    float roll = Random.Range(0, totalWeight);
                    foreach (var skill in readySkills)
                    {
                        roll -= skill.SkillData.SelectionWeight;
                        if (roll <= 0) { currentIntent = skill; break; }
                    }
                }
            }

            if (currentIntent != null)
            {
                float maxR = currentIntent.SkillData.MaxRange * distMult;
                float minR = currentIntent.SkillData.MinRange * distMult;

                if (dist > maxR) targetVelocity = dirToTarget * currentSpeed;
                else if (dist < minR) targetVelocity = -dirToTarget * currentSpeed;
                else
                {
                    targetVelocity = Vector2.zero;
                    PerformAttack(currentIntent, dirToTarget);
                    currentIntent = null;
                }
            }
            else targetVelocity = dirToTarget * (currentSpeed * 0.5f);
        }

        // 防追尾紧急手刹
        if (dist <= 0.01f && Vector2.Dot(targetVelocity, dirToTarget) > 0) targetVelocity = Vector2.zero;

        ExecutePhysicalMovement(targetVelocity, distMult);
    }

    private void ExecutePhysicalMovement(Vector2 desiredVelocity, float distMult)
    {
        // 👇【引擎接管拦截】：如果是 Staggered 状态，直接没收大脑的控制权！
        if (currentMoveState == MoveExecutionState.Staggered)
        {
            moveStateTimer -= Time.deltaTime;
            if (moveStateTimer <= 0)
            {
                currentMoveState = MoveExecutionState.Ready;
                rb.drag = 0f; // 恢复正常摩擦力
            }
            return;
        }

        if (MyData.MoveType == EnemyMoveType.Stationary)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (MyData.MoveType == EnemyMoveType.Normal)
        {
            rb.velocity = desiredVelocity;
            return;
        }

        if (currentMoveState == MoveExecutionState.Ready && desiredVelocity.sqrMagnitude < 0.01f)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        switch (currentMoveState)
        {
            case MoveExecutionState.Ready:
                currentMoveState = MoveExecutionState.Charging;
                moveStateTimer = MyData.MoveChargeTime;
                rb.velocity = Vector2.zero;
                break;

            case MoveExecutionState.Charging:
                rb.velocity = Vector2.zero;
                moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0)
                {
                    lockedMoveDirection = desiredVelocity.sqrMagnitude > 0.01f ? desiredVelocity.normalized : (Vector2)(currentTarget.position - transform.position).normalized;

                    if (MyData.MoveType == EnemyMoveType.ChargeDash)
                    {
                        currentMoveState = MoveExecutionState.Dashing;
                        moveStateTimer = MyData.DashDuration;
                    }
                    else if (MyData.MoveType == EnemyMoveType.Teleport)
                    {
                        Vector3 tpTarget = transform.position + (Vector3)(lockedMoveDirection * MyData.TeleportDistance * distMult);
                        transform.position = tpTarget;
                        currentMoveState = MoveExecutionState.Cooldown;
                        moveStateTimer = MyData.MoveCooldown;
                    }
                }
                break;

            case MoveExecutionState.Dashing:
                float baseSpeed = MyData.GetStat(StatType.MoveSpeed) * (CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f);
                rb.velocity = lockedMoveDirection * baseSpeed * MyData.DashSpeedMultiplier;

                moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0)
                {
                    currentMoveState = MoveExecutionState.Cooldown;
                    moveStateTimer = MyData.MoveCooldown;
                }
                break;

            case MoveExecutionState.Cooldown:
                rb.velocity = Vector2.zero;
                moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0)
                {
                    currentMoveState = MoveExecutionState.Ready;
                }
                break;
        }
    }

    private void TryRollAndFireSkills(float dist, Vector2 dirToTarget, float distMult)
    {
        var availableSkills = runtimeSkills.Where(s =>
            s.CurrentCooldown <= 0 &&
            dist <= (s.SkillData.MaxRange * distMult) &&
            dist >= (s.SkillData.MinRange * distMult)
        ).ToList();

        if (availableSkills.Count > 0)
        {
            float totalWeight = availableSkills.Sum(s => s.SkillData.SelectionWeight);
            float roll = Random.Range(0, totalWeight);
            RuntimeEnemySkill chosenSkill = null;

            foreach (var skill in availableSkills)
            {
                roll -= skill.SkillData.SelectionWeight;
                if (roll <= 0) { chosenSkill = skill; break; }
            }

            if (chosenSkill != null) PerformAttack(chosenSkill, dirToTarget);
        }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill, Vector2 attackDirection)
    {
        var skillData = rSkill.SkillData;

        float currentAtkSpeed = rSkill.DummyWeapon.GetStat(StatType.AttackSpeed);
        if (currentAtkSpeed <= 0) currentAtkSpeed = 50f;
        rSkill.CurrentCooldown = 100f / currentAtkSpeed;

        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        string critLog = isCrit ? "<color=#FFD700><b>(暴击!)</b></color>" : "";
        Debug.Log($"<color=#FF00FF>【敌人施法】</color> [{MyData.EnemyName}] 释放了 [{skillData.SkillName}]！| 判定伤害: {finalDmg:F1} {critLog}");

        ECAContext fireContext = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = currentTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
        foreach (var action in skillData.OnFireActions) if (action != null) action.Execute(fireContext);

        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));
            Projectile projectile = projObj.GetComponent<Projectile>();
            projectile.Fire(currentTarget, finalDmg, rSkill.DummyWeapon, isEnemy: true);
        }
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = currentTarget.position, PrimaryTarget = currentTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
            foreach (var action in skillData.OnHitActions) if (action != null) action.Execute(hitContext);

            Rigidbody2D targetRb = currentTarget.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null && skillData.KnockbackForce > 0)
            {
                targetRb.AddForce(attackDirection * skillData.KnockbackForce, ForceMode2D.Impulse);
            }
        }
    }

    private void ExecuteECAActions(List<ECAAction> actions, Transform target, RuntimeWeapon dummyWeapon)
    {
        if (actions == null || actions.Count == 0) return;
        ECAContext context = new ECAContext { ImpactPoint = this.transform.position, PrimaryTarget = target, BaseDamage = 0f, SourceWeapon = dummyWeapon };
        foreach (var action in actions) if (action != null) action.Execute(context);
    }

    // 👇【全新机制】：物理冲击接收器
    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        if (isDead) return;

        float mass = MyData != null ? Mathf.Max(MyData.GetStat(StatType.Mass), 0.5f) : 1f;
        float deltaV = impulse / mass;

        if (deltaV < 1.0f) return;

        float stunTime = deltaV * 0.05f;
        if (stunTime < 0.1f) stunTime = 0.1f;

        currentMoveState = MoveExecutionState.Staggered;
        moveStateTimer = stunTime;

        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void OnDrawGizmos()
    {
        if (MyData == null) return;

        float distMult = 1f;
        if (Application.isPlaying && CombatSandbox.Instance != null)
        {
            distMult = CombatSandbox.Instance.DistanceMultiplier;
        }

        Vector3 center = transform.position;

        if (MyData.MovementLogic == EnemyMovementStrategy.Artillery)
        {
            float hoverDist = MyData.HoverDistance * distMult;
            if (hoverDist > 0)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(center, hoverDist);
            }
        }

        if (MyData.Skills != null && MyData.Skills.Count > 0)
        {
            foreach (var skill in MyData.Skills)
            {
                if (skill == null) continue;
                float maxRange = skill.MaxRange * distMult;
                if (maxRange > 0)
                {
                    Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
                    Gizmos.DrawWireSphere(center, maxRange);
                }
                float minRange = skill.MinRange * distMult;
                if (minRange > 0)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                    Gizmos.DrawWireSphere(center, minRange);
                }
            }
        }
    }
}