// --- START OF FILE EnemyBrain.cs ---
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private Collider2D myHitboxCollider;

    private Transform currentTarget;
    private List<RuntimeEnemySkill> runtimeSkills = new List<RuntimeEnemySkill>();
    private RuntimeEnemySkill currentIntent = null;

    private enum MoveExecutionState { Ready, Charging, Dashing, Cooldown, Staggered }
    private MoveExecutionState currentMoveState = MoveExecutionState.Ready;
    private float moveStateTimer = 0f;
    private Vector2 lockedMoveDirection;

    // 👇【核心新增 1】：全局施法间隔 (Global Cooldown)
    private float globalActionTimer = 0f;

    private float lastFrameHP;
    private bool isDead = false;
    private HashSet<DamageReceiver> dashedVictims = new HashSet<DamageReceiver>();

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }

        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();

        float maxHP = MyData.GetStat(StatType.HP);
        float maxAP = MyData.GetStat(StatType.AP);
        myReceiver.Initialize(maxHP > 0 ? maxHP : 100f, maxAP);
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;

        GameObject visualHitboxNode = new GameObject("VisualAndHitbox");
        visualHitboxNode.transform.SetParent(this.transform, false);
        visualHitboxNode.layer = LayerMask.NameToLayer("Enemy_Hitbox");

        SpriteRenderer mainSr = visualHitboxNode.AddComponent<SpriteRenderer>();
        if (MyData.EnemySprite != null) mainSr.sprite = MyData.EnemySprite;
        visualHitboxNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier;

        myHitboxCollider = visualHitboxNode.AddComponent<BoxCollider2D>();
        myHitboxCollider.isTrigger = true;

        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.drag = 3f; rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);

        if (mainSr.sprite != null)
        {
            Vector2 realSize = mainSr.sprite.bounds.size * MyData.VisualScaleMultiplier;
            BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
            if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
            physicsCol.isTrigger = false;
            physicsCol.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f);
            physicsCol.offset = new Vector2(0f, -(realSize.y / 2f) + (physicsCol.size.y / 2f));

            DynamicDepthSorter sorter = gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(realSize.y / 2f);
        }

        foreach (var skillSO in MyData.Skills)
        {
            if (skillSO == null) continue;
            var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f };
            rSkill.DummyWeapon = new RuntimeWeapon { WeaponName = skillSO.SkillName, DeliveryType = skillSO.DeliveryType, ProjectilePrefab = skillSO.ProjectilePrefab };
            rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed;
            rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed;
            rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions);
            runtimeSkills.Add(rSkill);
        }

        ExecuteECAActions(MyData.OnSpawnActions, this.transform, null);
        myReceiver.OnEntityDeath += HandleDeathSequence;

        if (MyData.AnimController != null) visualHitboxNode.AddComponent<Animator>().runtimeAnimatorController = MyData.AnimController;

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim != null) { procAnim.SetTargetVisual(visualHitboxNode.transform); procAnim.RefreshBaseState(); }
    }

    // 👇【核心新增 2】：动态获取最终属性 (受减速/冰冻 Buff 影响！)
    private float GetFinalStat(StatType statType, float baseValue = 0f)
    {
        float val = baseValue == 0f ? MyData.GetStat(statType) : baseValue;
        BuffManager buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null && buffMgr.BuffStatModifiers.ContainsKey(statType))
        {
            val += buffMgr.BuffStatModifiers[statType];
        }
        return val;
    }

    private void Update()
    {
        if (isDead) return;

        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, this.transform, null);
            lastFrameHP = myReceiver.CurrentHP;
        }
        if (myReceiver.CurrentHP <= 0) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            rb.velocity = Vector2.zero; return;
        }

        // 全局施法硬直冷却
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;

        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) skill.CurrentCooldown -= Time.deltaTime;
        }

        currentTarget = GetTargetByStrategy(MyData.TargetingLogic);
        HandleMovementAndCombat();
    }

    // 👇【核心新增 3】：独立的策略索敌引擎
    private Transform GetTargetByStrategy(TargetingStrategy strategy)
    {
        var allPlayers = FindObjectsOfType<DamageReceiver>().Where(r => !r.isEnemy && r.CurrentHP > 0).ToList();
        if (allPlayers.Count == 0) return null;

        switch (strategy)
        {
            case TargetingStrategy.MaxHPHighest: return allPlayers.OrderByDescending(p => p.MaxHP).First().transform;
            case TargetingStrategy.MaxHPLowest: return allPlayers.OrderBy(p => p.MaxHP).First().transform;
            case TargetingStrategy.CurrentHPHighest: return allPlayers.OrderByDescending(p => p.CurrentHP).First().transform;
            case TargetingStrategy.CurrentHPLowest: return allPlayers.OrderBy(p => p.CurrentHP).First().transform;
            case TargetingStrategy.Nearest:
            default: return allPlayers.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).First().transform;
        }
    }

    // 提纯的测距引擎 (基于外壳)
    private float CalculateDistanceToTarget(Transform target, out Vector2 dirToTarget)
    {
        if (target == null) { dirToTarget = Vector2.zero; return 0f; }
        Vector2 myCenter = myHitboxCollider != null ? (Vector2)myHitboxCollider.bounds.center : (Vector2)transform.position;
        Collider2D targetCol = target.GetComponentInChildren<Collider2D>();

        if (targetCol != null)
        {
            Vector2 targetEdgePoint = targetCol.ClosestPoint(myCenter);
            dirToTarget = (targetEdgePoint - myCenter).normalized;
            if (dirToTarget == Vector2.zero) dirToTarget = (target.position - transform.position).normalized;
            return Vector2.Distance(myCenter, targetEdgePoint);
        }
        dirToTarget = (target.position - transform.position).normalized;
        return Vector2.Distance(myCenter, target.position);
    }

    private void HandleMovementAndCombat()
    {
        if (currentTarget == null || MyData.MoveType == EnemyMoveType.Stationary)
        {
            ExecutePhysicalMovement(Vector2.zero, 1f); return;
        }

        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        float distToMain = CalculateDistanceToTarget(currentTarget, out Vector2 dirToMainTarget);

        // 动态移速 (吃减速 Buff)
        float currentSpeed = GetFinalStat(StatType.MoveSpeed) * speedMult;
        if (currentSpeed < 0.1f) currentSpeed = 0.1f;

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
            // 1. 如果手里没牌，且不在施法僵直中，抽一张牌（确定意图）！
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

            // 2. 如果手里有牌，死死执行这个意图！
            if (currentIntent != null)
            {
                // 极其智能：使用这张牌专属的索敌逻辑去找目标！
                Transform intentTarget = currentIntent.SkillData.OverrideTargeting ? GetTargetByStrategy(currentIntent.SkillData.SkillTargetingLogic) : currentTarget;

                if (intentTarget != null)
                {
                    float intentDist = CalculateDistanceToTarget(intentTarget, out Vector2 intentDir);
                    float maxR = currentIntent.SkillData.MaxRange * distMult;
                    float minR = currentIntent.SkillData.MinRange * distMult;

                    // 走位拉扯，直到进入这张牌的射程
                    if (intentDist > maxR) targetVelocity = intentDir * currentSpeed;
                    else if (intentDist < minR) targetVelocity = -intentDir * currentSpeed;
                    else
                    {
                        // 进入射程，踩刹车，打出底牌！
                        targetVelocity = Vector2.zero;
                        PerformAttack(currentIntent);
                        currentIntent = null; // 打完牌，清空意图，等待下一轮抽卡
                    }
                }
                else
                {
                    currentIntent = null; // 目标死了或消失了，把牌撕了重新抽
                }
            }
            else
            {
                // 手里没牌又在冷却时，缓慢向着最近的敌人游走压迫
                targetVelocity = dirToMainTarget * (currentSpeed * 0.5f);
            }
        }
    }

    private void TryRollAndFireSkills(float distMult)
    {
        // 施法僵直中，不准抽卡！(这是拉扯感的来源)
        if (globalActionTimer > 0) return;

        var availableSkills = new List<RuntimeEnemySkill>();

        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) continue;

            // 👇 独立索敌覆写！
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

        // 👇 攻速与施法僵直 (吃减速 Buff！)
        float baseAtkSpeed = rSkill.DummyWeapon.GetStat(StatType.AttackSpeed);
        if (baseAtkSpeed <= 0) baseAtkSpeed = 50f;
        float finalAtkSpeed = GetFinalStat(StatType.AttackSpeed, baseAtkSpeed);
        if (finalAtkSpeed < 1f) finalAtkSpeed = 1f;

        float cd = GameFormulas.CalcCooldown(finalAtkSpeed);
        rSkill.CurrentCooldown = cd;
        // 全局施法间隔：占用技能冷却的 40% 时间，这期间怪物发呆或凭惯性滑行！
        globalActionTimer = cd * 0.4f;

        CalculateDistanceToTarget(actualTarget, out Vector2 attackDir);

        // ==========================================
        // 👇【核心新增】：战术位移技能 (Tactical Dash)
        // ==========================================
        if (skillData.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            Vector2 dashDir = Vector2.zero;
            if (skillData.DashDirection == TacticalDashDirection.AwayFromTarget) dashDir = -attackDir;
            else if (skillData.DashDirection == TacticalDashDirection.TowardsTarget) dashDir = attackDir;
            else dashDir = new Vector2(-attackDir.y, attackDir.x); // 侧滑

            // 给自己施加物理冲量进行闪避！
            ApplyImpulse(dashDir, skillData.DashImpulse);

            Debug.Log($"<color=#00FFFF>【战术拉扯】</color> [{MyData.EnemyName}] 释放了 [{skillData.SkillName}]，进行战术机动！");

            ECAContext dashCtx = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = actualTarget, SourceEntity = transform, IsEnemyFire = true };
            ExecuteECAActions(skillData.OnFireActions, actualTarget, rSkill.DummyWeapon);
            return;
        }

        // === 正常攻击逻辑 ===
        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        string critLog = isCrit ? "<color=#FFD700><b>(暴击!)</b></color>" : "";
        Debug.Log($"<color=#FF00FF>【敌人施法】</color> [{MyData.EnemyName}] 释放了 [{skillData.SkillName}]！| 判定伤害: {finalDmg:F1} {critLog}");

        ECAContext fireContext = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
        foreach (var action in skillData.OnFireActions) if (action != null) action.Execute(fireContext);

        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));
            projObj.GetComponent<Projectile>().Fire(actualTarget, finalDmg, rSkill.DummyWeapon, true, isCrit);
        }
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = actualTarget.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
            foreach (var action in skillData.OnHitActions) if (action != null) action.Execute(hitContext);
            Rigidbody2D targetRb = actualTarget.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null && skillData.KnockbackForce > 0) targetRb.AddForce(attackDir * skillData.KnockbackForce, ForceMode2D.Impulse);
        }
    }

    private void ExecutePhysicalMovement(Vector2 desiredVelocity, float distMult)
    {
        if (currentMoveState == MoveExecutionState.Staggered)
        {
            moveStateTimer -= Time.deltaTime;
            if (moveStateTimer <= 0) { currentMoveState = MoveExecutionState.Ready; rb.drag = 3f; } // 恢复正常阻力
            return;
        }

        // 核心惯性滑行：如果在施法发呆期间 (globalActionTimer > 0)，不再主动给速度，任由物理引擎用 drag 减速！
        if (globalActionTimer > 0) return;

        if (MyData.MoveType == EnemyMoveType.Stationary) { rb.velocity = Vector2.zero; return; }
        if (MyData.MoveType == EnemyMoveType.Normal) { rb.velocity = desiredVelocity; return; }

        if (currentMoveState == MoveExecutionState.Ready && desiredVelocity.sqrMagnitude < 0.01f) { rb.velocity = Vector2.zero; return; }

        switch (currentMoveState)
        {
            case MoveExecutionState.Ready:
                currentMoveState = MoveExecutionState.Charging; moveStateTimer = MyData.MoveChargeTime; rb.velocity = Vector2.zero;
                break;
            case MoveExecutionState.Charging:
                rb.velocity = Vector2.zero; moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0)
                {
                    lockedMoveDirection = desiredVelocity.sqrMagnitude > 0.01f ? desiredVelocity.normalized : (Vector2)(currentTarget.position - transform.position).normalized;
                    if (MyData.MoveType == EnemyMoveType.ChargeDash) { currentMoveState = MoveExecutionState.Dashing; moveStateTimer = MyData.DashDuration; }
                    else if (MyData.MoveType == EnemyMoveType.Teleport)
                    {
                        transform.position = transform.position + (Vector3)(lockedMoveDirection * MyData.TeleportDistance * distMult);
                        currentMoveState = MoveExecutionState.Cooldown; moveStateTimer = MyData.MoveCooldown;
                    }
                }
                break;
            case MoveExecutionState.Dashing:
                float baseSpeed = GetFinalStat(StatType.MoveSpeed) * (CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f);
                rb.velocity = lockedMoveDirection * baseSpeed * MyData.DashSpeedMultiplier;
                moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0) { currentMoveState = MoveExecutionState.Cooldown; moveStateTimer = MyData.MoveCooldown; }
                break;
            case MoveExecutionState.Cooldown:
                rb.velocity = Vector2.zero; moveStateTimer -= Time.deltaTime;
                if (moveStateTimer <= 0) currentMoveState = MoveExecutionState.Ready;
                break;
        }
    }

    private void ExecuteECAActions(List<ECAAction> actions, Transform target, RuntimeWeapon dummyWeapon)
    {
        if (actions == null || actions.Count == 0) return;
        ECAContext context = new ECAContext { ImpactPoint = this.transform.position, PrimaryTarget = target, BaseDamage = 0f, SourceWeapon = dummyWeapon, IsEnemyFire = true, SourceEntity = this.transform };
        foreach (var action in actions) if (action != null) action.Execute(context);
    }

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

        rb.drag = 5f; // 受击时临时增大阻力防止飞出屏幕
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void LateUpdate() { EnforceArenaBounds(); }
    private void EnforceArenaBounds() { /* ... 保持原样 ... */ }
    private void HandleDeathSequence() { /* ... 保持原样 ... */ }
    private IEnumerator CorpseDecayRoutine() { yield return null; /* ... 保持原样 ... */ }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead) return;

        // 只有在“冲刺态”下，撞击才具有毁灭性！
        if (currentMoveState == MoveExecutionState.Dashing)
        {
            // 撞到了谁？
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();

            // 如果撞到的是玩家机甲，且这次冲锋还没碾过他
            if (victim != null && !victim.isEnemy && !dashedVictims.Contains(victim))
            {
                dashedVictims.Add(victim);

                // 1. 动能伤害公式：自身质量 (Mass) 乘以 冲刺速度倍率，越重越痛！
                float myMass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);
                float kineticDamage = myMass * MyData.DashSpeedMultiplier * 5f;

                victim.TakeDamage(kineticDamage, MyData.EnemyName + " (野蛮冲撞)");

                // 2. 动能击飞：把玩家撞得倒退滑行！
                ChimeraAIController playerAI = victim.GetComponent<ChimeraAIController>();
                if (playerAI != null)
                {
                    Vector2 knockbackDir = (victim.transform.position - transform.position).normalized;
                    // 施加巨大冲量
                    playerAI.ApplyImpulse(knockbackDir, myMass * 100f);
                }

                // 3. 极致反馈：撞击瞬间屏幕震动！
                if (ScreenEffectManager.Instance != null)
                {
                    ScreenEffectManager.Instance.TriggerShake(0.4f, 0.2f);
                }

                Debug.Log($"<color=#FF4500>【破阵碾压】</color> {MyData.EnemyName} 犹如全速行驶的泥头车，将机甲撞飞并造成 {kineticDamage:F1} 点物理伤害！");
            }
        }
        else
        {
            // 如果没在冲刺，就清空受害者名单，为下一次冲锋做准备
            dashedVictims.Clear();
        }
    }
}