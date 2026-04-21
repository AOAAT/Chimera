using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ==========================================
// 1. 运行时怪物技能实例 (必须定义在此)
// ==========================================
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

        // 【血量自由】：完全读取图纸，不再有 100HP 的强制保底
        float initialHP = MyData.GetStat(StatType.HP);
        float initialAP = MyData.GetStat(StatType.AP);
        myReceiver.Initialize(initialHP > 0 ? initialHP : 1f, initialAP);
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;

        // 初始化视觉、物理和技能池
        SetupVisuals();
        SetupPhysics();
        InitializeSkills();

        // 触发出生 ECA 信号
        ExecuteECAActions(MyData.OnSpawnActions, null);

        myReceiver.OnEntityDeath += HandleDeathSequence;
    }

    private void SetupVisuals()
    {
        GameObject visualHitboxNode = null;
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

        myHitboxCollider = visualHitboxNode.GetComponent<BoxCollider2D>() ?? visualHitboxNode.AddComponent<BoxCollider2D>();
        myHitboxCollider.isTrigger = true;

        if (MyData.AnimController != null)
        {
            Animator anim = visualHitboxNode.GetComponent<Animator>() ?? visualHitboxNode.AddComponent<Animator>();
            anim.runtimeAnimatorController = MyData.AnimController;
        }

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>();
        procAnim.SetTargetVisual(visualHitboxNode.transform);
        procAnim.RefreshBaseState();
    }

    private void SetupPhysics()
    {
        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.drag = 3f;
        rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 realSize = sr.sprite.bounds.size * MyData.VisualScaleMultiplier;
            BoxCollider2D physicsCol = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>();
            physicsCol.isTrigger = false;
            physicsCol.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f);
            physicsCol.offset = new Vector2(0f, -(realSize.y / 2f) + (physicsCol.size.y / 2f));

            DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(realSize.y / 2f);
        }
    }

    private void InitializeSkills()
    {
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
    }

    private void Update()
    {
        if (isDead) return;

        // --- 核心：受击 ECA 触发 (碎块由此产生) ---
        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, null);
            lastFrameHP = myReceiver.CurrentHP;
        }

        if (myReceiver.CurrentHP <= 0) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            rb.velocity = Vector2.zero;
            return;
        }

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

        if (MyData.MovementLogic == EnemyMovementStrategy.Swarm)
        {
            float stopDist = Mathf.Max(MyData.StopDistance * distMult, 0.8f);
            if (distToMain > stopDist) targetVelocity = dirToMainTarget * currentSpeed;
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
            HandleIntentDrivenAI(distMult, currentSpeed, dirToMainTarget);
        }

        ExecutePhysicalMovement(targetVelocity, distMult);
    }

    private void HandleIntentDrivenAI(float distMult, float currentSpeed, Vector2 dirToMain)
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

                if (intentDist > maxR) rb.velocity = intentDir * currentSpeed;
                else if (intentDist < minR) rb.velocity = -intentDir * currentSpeed;
                else
                {
                    rb.velocity = Vector2.zero;
                    PerformAttack(currentIntent);
                    currentIntent = null;
                }
            }
            else currentIntent = null;
        }
        else
        {
            rb.velocity = dirToMain * (currentSpeed * 0.5f);
        }
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
                if (roll <= 0) { PerformAttack(skill); break; }
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

        if (skillData.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            Vector2 dashDir = (skillData.DashDirection == TacticalDashDirection.AwayFromTarget) ? -attackDir : (skillData.DashDirection == TacticalDashDirection.TowardsTarget ? attackDir : new Vector2(-attackDir.y, attackDir.x));
            ApplyImpulse(dashDir, skillData.DashImpulse * speedMult);
            return;
        }

        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));

            Projectile pScript = projObj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 参数对齐：目标, 伤害, 武器, 玩家黑盒(怪物传null), 自身, 是否怪弹, 是否暴击, 代际, 是否奶弹
                pScript.Fire(actualTarget, finalDmg, rSkill.DummyWeapon, null, this.transform, true, isCrit, 0, false);
            }
        }
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = actualTarget.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsEnemyFire = true, SourceEntity = this.transform };
            foreach (var action in skillData.OnHitActions) action.Execute(hitContext);

            Rigidbody2D targetRb = actualTarget.GetComponentInParent<Rigidbody2D>();
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

        UpdateComplexMovement(desiredVelocity, distMult);
    }

    private void UpdateComplexMovement(Vector2 desiredVelocity, float distMult)
    {
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
                    if (MyData.MoveType == EnemyMoveType.ChargeDash) { currentMoveState = MoveExecutionState.Dashing; moveStateTimer = MyData.DashDuration; }
                    else if (MyData.MoveType == EnemyMoveType.Teleport) { transform.position += (Vector3)(lockedMoveDirection * MyData.TeleportDistance * distMult); currentMoveState = MoveExecutionState.Cooldown; moveStateTimer = MyData.MoveCooldown; }
                }
                break;
            case MoveExecutionState.Dashing:
                float baseSpeed = GetFinalStat(StatType.MoveSpeed) * (CombatSandbox.Instance?.SpeedMultiplier ?? 1f);
                rb.velocity = lockedMoveDirection * baseSpeed * MyData.DashSpeedMultiplier;
                moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0) { currentMoveState = MoveExecutionState.Cooldown; moveStateTimer = MyData.MoveCooldown; }
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

    // ==========================================
    // 信号分发与死亡管线
    // ==========================================
    private void ExecuteECAActions(List<ECAAction> actions, RuntimeWeapon w)
    {
        if (actions == null || actions.Count == 0) return;
        ECAContext c = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = this.transform, SourceWeapon = w, IsEnemyFire = true, SourceEntity = this.transform };
        foreach (var x in actions) if (x != null) x.Execute(c);
    }

    // --- 修改 EnemyBrain.cs 的 HandleDeathSequence 方法 ---
    private void HandleDeathSequence()
    {
        if (isDead) return;
        isDead = true;

        // 1. 物理与动画停摆逻辑保持不变...
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        rb.simulated = false;

        // 2. 👇【核心修复】：在实体彻底消失前，触发所有 Buff 的死亡管线
        BuffManager bm = GetComponent<BuffManager>();
        if (bm != null)
        {
            ECAContext deathContext = new ECAContext
            {
                ImpactPoint = transform.position,
                PrimaryTarget = this.transform,
                SourceEntity = this.transform, // 此时自己是来源
                IsEnemyFire = true
            };
            bm.TriggerHolderDeathActions(deathContext);
        }

        // 3. 原有的怪物图纸死亡积木触发（依然保留，作为双轨制）
        ExecuteECAActions(MyData.OnDeathActions, null);

        // 4. 尸体淡出逻辑...
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
            foreach (var s in srs) { Color c = s.color; c.a = a; s.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }

    // --- 辅助方法 ---
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
}