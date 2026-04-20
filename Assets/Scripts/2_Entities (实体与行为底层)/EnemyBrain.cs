using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 运行时怪物技能实例
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
    private Collider2D myHitboxCollider;

    private Transform currentTarget;
    private List<RuntimeEnemySkill> runtimeSkills = new List<RuntimeEnemySkill>();
    private RuntimeEnemySkill currentIntent = null;

    private enum MoveExecutionState { Ready, Charging, Dashing, Cooldown, Staggered }
    private MoveExecutionState currentMoveState = MoveExecutionState.Ready;
    private float moveStateTimer = 0f;
    private Vector2 lockedMoveDirection;

    private float globalActionTimer = 0f;
    private float lastFrameHP;
    private bool isDead = false;
    private HashSet<DamageReceiver> dashedVictims = new HashSet<DamageReceiver>();

    private void Start()
    {
        if (MyData == null)
        {
            enabled = false;
            return;
        }

        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();

        // 初始化血条
        float maxHP = MyData.GetStat(StatType.HP);
        float maxAP = MyData.GetStat(StatType.AP);
        myReceiver.Initialize(maxHP > 0 ? maxHP : 100f, maxAP);
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;

        // --- 视觉节点初始化 ---
        GameObject visualHitboxNode = null;
        SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
        if (rootSr != null) Destroy(rootSr);

        SpriteRenderer existingChildSr = GetComponentInChildren<SpriteRenderer>();
        if (existingChildSr != null && existingChildSr.gameObject != this.gameObject)
        {
            visualHitboxNode = existingChildSr.gameObject;
        }
        else
        {
            visualHitboxNode = new GameObject("VisualAndHitbox");
            visualHitboxNode.transform.SetParent(this.transform, false);
            visualHitboxNode.AddComponent<SpriteRenderer>();
        }

        if (MyData.EnemySprite != null)
        {
            visualHitboxNode.GetComponent<SpriteRenderer>().sprite = MyData.EnemySprite;
        }

        visualHitboxNode.layer = LayerMask.NameToLayer("Enemy_Hitbox");
        visualHitboxNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier;

        myHitboxCollider = visualHitboxNode.GetComponent<BoxCollider2D>();
        if (myHitboxCollider == null) myHitboxCollider = visualHitboxNode.AddComponent<BoxCollider2D>();
        myHitboxCollider.isTrigger = true;

        // --- 物理层配置 ---
        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.drag = 3f;
        rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);

        SpriteRenderer mainSr = visualHitboxNode.GetComponent<SpriteRenderer>();
        if (mainSr != null && mainSr.sprite != null)
        {
            Vector2 realSize = mainSr.sprite.bounds.size * MyData.VisualScaleMultiplier;
            BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
            if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();

            physicsCol.isTrigger = false;
            physicsCol.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f);
            physicsCol.offset = new Vector2(0f, -(realSize.y / 2f) + (physicsCol.size.y / 2f));

            DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>();
            if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(realSize.y / 2f);
        }

        // --- 技能初始化 ---
        foreach (var skillSO in MyData.Skills)
        {
            if (skillSO == null) continue;
            var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f };
            rSkill.DummyWeapon = new RuntimeWeapon
            {
                WeaponName = skillSO.SkillName,
                DeliveryType = skillSO.DeliveryType,
                ProjectilePrefab = skillSO.ProjectilePrefab
            };
            rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed;
            rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed;
            rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions);
            runtimeSkills.Add(rSkill);
        }

        ExecuteECAActions(MyData.OnSpawnActions, this.transform, null);
        myReceiver.OnEntityDeath += HandleDeathSequence;

        if (MyData.AnimController != null)
        {
            Animator anim = visualHitboxNode.GetComponent<Animator>();
            if (anim == null) anim = visualHitboxNode.AddComponent<Animator>();
            anim.runtimeAnimatorController = MyData.AnimController;
        }

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim != null)
        {
            procAnim.SetTargetVisual(visualHitboxNode.transform);
            procAnim.RefreshBaseState();
        }
    }

    private void Update()
    {
        if (isDead) return;

        // 受击反馈
        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, this.transform, null);
            lastFrameHP = myReceiver.CurrentHP;
        }

        if (myReceiver.CurrentHP <= 0) return;

        // 战斗未开启时静止
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 计时器更新
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;
        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) skill.CurrentCooldown -= Time.deltaTime;
        }

        currentTarget = GetTargetByStrategy(MyData.TargetingLogic);
        HandleMovementAndCombat();
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

        float distToMain = CalculateDistanceToTarget(currentTarget, out Vector2 dirToMainTarget);
        float currentSpeed = GetFinalStat(StatType.MoveSpeed) * speedMult;

        Vector2 targetVelocity = Vector2.zero;

        // --- 行为树分支 ---
        if (MyData.MovementLogic == EnemyMovementStrategy.Swarm)
        {
            float stopDist = Mathf.Max(MyData.StopDistance * distMult, 0.8f);
            if (distToMain > stopDist)
            {
                targetVelocity = dirToMainTarget * currentSpeed;
            }
            TryRollAndFireSkills(distMult);
        }
        else if (MyData.MovementLogic == EnemyMovementStrategy.Artillery)
        {
            float hoverDist = MyData.HoverDistance * distMult;
            if (distToMain > hoverDist + 0.5f) targetVelocity = dirToMainTarget * currentSpeed;
            else if (distToMain < hoverDist - 0.5f) targetVelocity = -dirToMainTarget * currentSpeed;
            TryRollAndFireSkills(distMult);
        }
        else if (MyData.MovementLogic == EnemyMovementStrategy.IntentDriven)
        {
            if (currentIntent == null && globalActionTimer <= 0)
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
                Transform intentT = currentIntent.SkillData.OverrideTargeting ? GetTargetByStrategy(currentIntent.SkillData.SkillTargetingLogic) : currentTarget;
                if (intentT != null)
                {
                    float intentDist = CalculateDistanceToTarget(intentT, out Vector2 intentDir);
                    float maxR = currentIntent.SkillData.MaxRange * distMult;
                    float minR = currentIntent.SkillData.MinRange * distMult;

                    if (intentDist > maxR) targetVelocity = intentDir * currentSpeed;
                    else if (intentDist < minR) targetVelocity = -intentDir * currentSpeed;
                    else
                    {
                        targetVelocity = Vector2.zero;
                        PerformAttack(currentIntent);
                        currentIntent = null;
                    }
                }
                else currentIntent = null;
            }
            else
            {
                targetVelocity = dirToMainTarget * (currentSpeed * 0.5f);
            }
        }

        ExecutePhysicalMovement(targetVelocity, distMult);
    }

    private void TryRollAndFireSkills(float distMult)
    {
        if (globalActionTimer > 0) return;

        var availableSkills = new List<RuntimeEnemySkill>();
        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) continue;

            Transform targetForSkill = skill.SkillData.OverrideTargeting ? GetTargetByStrategy(skill.SkillData.SkillTargetingLogic) : currentTarget;
            if (targetForSkill == null) continue;

            float dist = CalculateDistanceToTarget(targetForSkill, out Vector2 dir);
            // 度量衡应用：判定射程
            if (dist <= (skill.SkillData.MaxRange * distMult) && dist >= (skill.SkillData.MinRange * distMult))
            {
                availableSkills.Add(skill);
            }
        }

        if (availableSkills.Count > 0)
        {
            float totalWeight = availableSkills.Sum(s => s.SkillData.SelectionWeight);
            float roll = Random.Range(0, totalWeight);
            foreach (var skill in availableSkills)
            {
                roll -= skill.SkillData.SelectionWeight;
                if (roll <= 0)
                {
                    PerformAttack(skill);
                    break;
                }
            }
        }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill)
    {
        var skillData = rSkill.SkillData;
        Transform actualTarget = skillData.OverrideTargeting ? GetTargetByStrategy(skillData.SkillTargetingLogic) : currentTarget;
        if (actualTarget == null) return;

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        float cd = GameFormulas.CalcCooldown(GetFinalStat(StatType.AttackSpeed, rSkill.DummyWeapon.GetStat(StatType.AttackSpeed)));
        rSkill.CurrentCooldown = cd;
        globalActionTimer = cd * 0.4f;

        CalculateDistanceToTarget(actualTarget, out Vector2 attackDir);

        // --- 战术位移 ---
        if (skillData.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            Vector2 dashDir = (skillData.DashDirection == TacticalDashDirection.AwayFromTarget) ? -attackDir : (skillData.DashDirection == TacticalDashDirection.TowardsTarget ? attackDir : new Vector2(-attackDir.y, attackDir.x));
            // 度量衡应用：冲量
            ApplyImpulse(dashDir, skillData.DashImpulse * speedMult);
            ExecuteECAActions(skillData.OnFireActions, actualTarget, rSkill.DummyWeapon);
            return;
        }

        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        // --- 远程攻击 ---
        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));
            projObj.GetComponent<Projectile>().Fire(actualTarget, finalDmg, rSkill.DummyWeapon, true, isCrit);
        }
        // --- 近战攻击 ---
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = actualTarget.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true, SourceEntity = this.transform };
            foreach (var action in skillData.OnHitActions) action.Execute(hitContext);

            Rigidbody2D targetRb = actualTarget.GetComponentInParent<Rigidbody2D>();
            // 度量衡应用：击退力
            if (targetRb != null && skillData.KnockbackForce > 0)
            {
                targetRb.AddForce(attackDir * skillData.KnockbackForce * speedMult, ForceMode2D.Impulse);
            }
        }
    }

    private void ExecutePhysicalMovement(Vector2 desiredVelocity, float distMult)
    {
        if (currentMoveState == MoveExecutionState.Staggered)
        {
            moveStateTimer -= Time.deltaTime;
            if (moveStateTimer <= 0) { currentMoveState = MoveExecutionState.Ready; rb.drag = 3f; }
            return;
        }

        if (globalActionTimer > 0 || MyData.MoveType == EnemyMoveType.Stationary)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (MyData.MoveType == EnemyMoveType.Normal)
        {
            rb.velocity = desiredVelocity;
            return;
        }

        switch (currentMoveState)
        {
            case MoveExecutionState.Ready:
                if (desiredVelocity.sqrMagnitude > 0.01f)
                {
                    currentMoveState = MoveExecutionState.Charging;
                    moveStateTimer = MyData.MoveChargeTime;
                    rb.velocity = Vector2.zero;
                }
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
                        // 度量衡应用：传送距离
                        transform.position += (Vector3)(lockedMoveDirection * MyData.TeleportDistance * distMult);
                        currentMoveState = MoveExecutionState.Cooldown;
                        moveStateTimer = MyData.MoveCooldown;
                    }
                }
                break;

            case MoveExecutionState.Dashing:
                float baseSpeed = GetFinalStat(StatType.MoveSpeed) * (CombatSandbox.Instance?.SpeedMultiplier ?? 1f);
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
                if (moveStateTimer <= 0) currentMoveState = MoveExecutionState.Ready;
                break;
        }
    }

    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        if (isDead) return;
        float mass = Mathf.Max(rb.mass, 0.5f);
        float deltaV = impulse / mass;
        if (deltaV < 1.0f) return;

        currentMoveState = MoveExecutionState.Staggered;
        moveStateTimer = Mathf.Max(deltaV * 0.05f, 0.1f);
        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private float CalculateDistanceToTarget(Transform target, out Vector2 dir)
    {
        if (target == null) { dir = Vector2.zero; return 0f; }
        Vector2 myCenter = myHitboxCollider != null ? (Vector2)myHitboxCollider.bounds.center : (Vector2)transform.position;
        Collider2D targetCol = target.GetComponentInChildren<Collider2D>();
        if (targetCol != null)
        {
            Vector2 edge = targetCol.ClosestPoint(myCenter);
            dir = (edge - myCenter).normalized;
            if (dir == Vector2.zero) dir = (target.position - transform.position).normalized;
            return Vector2.Distance(myCenter, edge);
        }
        dir = (target.position - transform.position).normalized;
        return Vector2.Distance(myCenter, target.position);
    }

    private Transform GetTargetByStrategy(TargetingStrategy s)
    {
        var p = FindObjectsOfType<DamageReceiver>().Where(r => !r.isEnemy && r.CurrentHP > 0).ToList();
        if (p.Count == 0) return null;
        switch (s)
        {
            case TargetingStrategy.MaxHPHighest: return p.OrderByDescending(x => x.MaxHP).First().transform;
            case TargetingStrategy.MaxHPLowest: return p.OrderBy(x => x.MaxHP).First().transform;
            case TargetingStrategy.CurrentHPHighest: return p.OrderByDescending(x => x.CurrentHP).First().transform;
            case TargetingStrategy.CurrentHPLowest: return p.OrderBy(x => x.CurrentHP).First().transform;
            default: return p.OrderBy(x => Vector3.Distance(transform.position, x.transform.position)).First().transform;
        }
    }

    private float GetFinalStat(StatType t, float b = 0)
    {
        float v = b == 0 ? MyData.GetStat(t) : b;
        var m = GetComponent<BuffManager>();
        if (m != null && m.BuffStatModifiers.ContainsKey(t)) v += m.BuffStatModifiers[t];
        return v;
    }

    private void ExecuteECAActions(List<ECAAction> a, Transform t, RuntimeWeapon w)
    {
        if (a == null) return;
        ECAContext c = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = t, SourceWeapon = w, IsEnemyFire = true, SourceEntity = transform };
        foreach (var x in a) if (x != null) x.Execute(c);
    }

    private void HandleDeathSequence()
    {
        if (isDead) return;
        isDead = true;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        rb.simulated = false;

        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        foreach (var a in GetComponentsInChildren<Animator>()) a.enabled = false;
        if (GetComponent<ProceduralAnimator2D>() != null) GetComponent<ProceduralAnimator2D>().StopAnimation();

        ExecuteECAActions(MyData.OnDeathActions, transform, null);
        gameObject.layer = LayerMask.NameToLayer("Floor");
        StartCoroutine(CorpseDecayRoutine());
    }

    private IEnumerator CorpseDecayRoutine()
    {
        yield return new WaitForSeconds(MyData.CorpseLingerTime);
        float f = 2f, e = 0f;
        var srs = GetComponentsInChildren<SpriteRenderer>();
        while (e < f)
        {
            e += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, e / f);
            foreach (var s in srs)
            {
                Color c = s.color;
                c.a = a;
                s.color = c; // 已修复：这里之前错误写成了 sr.color
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}