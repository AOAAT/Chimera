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

        // ==========================================
        // 👇【核心修复】：智能贴图接管！绝对不产生影分身！
        // ==========================================
        GameObject visualHitboxNode = null;

        // 1. 先把挂在根节点的贴图无情删掉 (如果有的话)
        SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
        if (rootSr != null) Destroy(rootSr);

        // 2. 寻找子节点里是不是已经有贴图了？(比如测试台生成的)
        SpriteRenderer existingChildSr = GetComponentInChildren<SpriteRenderer>();

        if (existingChildSr != null && existingChildSr.gameObject != this.gameObject)
        {
            // 如果有，直接征用它！
            visualHitboxNode = existingChildSr.gameObject;
            if (MyData.EnemySprite != null) existingChildSr.sprite = MyData.EnemySprite;
        }
        else
        {
            // 3. 如果实在没有，再自己建一个！
            visualHitboxNode = new GameObject("VisualAndHitbox");
            visualHitboxNode.transform.SetParent(this.transform, false);
            SpriteRenderer newSr = visualHitboxNode.AddComponent<SpriteRenderer>();
            if (MyData.EnemySprite != null) newSr.sprite = MyData.EnemySprite;
        }

        visualHitboxNode.layer = LayerMask.NameToLayer("Enemy_Hitbox");
        visualHitboxNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier;

        myHitboxCollider = visualHitboxNode.GetComponent<BoxCollider2D>();
        if (myHitboxCollider == null) myHitboxCollider = visualHitboxNode.AddComponent<BoxCollider2D>();
        myHitboxCollider.isTrigger = true;

        // ==========================================
        // 物理层配置
        // ==========================================
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

            DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
            if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(realSize.y / 2f);
        }

        // ==========================================
        // 技能初始化
        // ==========================================
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

        if (MyData.AnimController != null)
        {
            Animator anim = visualHitboxNode.GetComponent<Animator>();
            if (anim == null) anim = visualHitboxNode.AddComponent<Animator>();
            anim.runtimeAnimatorController = MyData.AnimController;
        }

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim != null) { procAnim.SetTargetVisual(visualHitboxNode.transform); procAnim.RefreshBaseState(); }

        Debug.Log($"<color=#FF4500>【怪物异变参数】</color> [{MyData.EnemyName}] 质量(Mass): <color=white>{rb.mass:F1}</color> | 基础移速: <color=white>{MyData.GetStat(StatType.MoveSpeed):F2} m/s</color>");
    }

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

        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;

        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) skill.CurrentCooldown -= Time.deltaTime;
        }

        currentTarget = GetTargetByStrategy(MyData.TargetingLogic);
        HandleMovementAndCombat();
    }

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
                Transform intentTarget = currentIntent.SkillData.OverrideTargeting ? GetTargetByStrategy(currentIntent.SkillData.SkillTargetingLogic) : currentTarget;

                if (intentTarget != null)
                {
                    float intentDist = CalculateDistanceToTarget(intentTarget, out Vector2 intentDir);
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
                else
                {
                    currentIntent = null;
                }
            }
            else
            {
                targetVelocity = dirToMainTarget * (currentSpeed * 0.5f);
            }
        }

        if (distToMain <= 0.01f && Vector2.Dot(targetVelocity, dirToMainTarget) > 0) targetVelocity = Vector2.zero;
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

        float baseAtkSpeed = rSkill.DummyWeapon.GetStat(StatType.AttackSpeed);
        if (baseAtkSpeed <= 0) baseAtkSpeed = 50f;
        float finalAtkSpeed = GetFinalStat(StatType.AttackSpeed, baseAtkSpeed);
        if (finalAtkSpeed < 1f) finalAtkSpeed = 1f;

        float cd = GameFormulas.CalcCooldown(finalAtkSpeed);
        rSkill.CurrentCooldown = cd;
        globalActionTimer = cd * 0.4f;

        CalculateDistanceToTarget(actualTarget, out Vector2 attackDir);

        if (skillData.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            Vector2 dashDir = Vector2.zero;
            if (skillData.DashDirection == TacticalDashDirection.AwayFromTarget) dashDir = -attackDir;
            else if (skillData.DashDirection == TacticalDashDirection.TowardsTarget) dashDir = attackDir;
            else dashDir = new Vector2(-attackDir.y, attackDir.x);

            ApplyImpulse(dashDir, skillData.DashImpulse);
            ECAContext dashCtx = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = actualTarget, SourceEntity = transform, IsEnemyFire = true };
            ExecuteECAActions(skillData.OnFireActions, actualTarget, rSkill.DummyWeapon);
            return;
        }

        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        ECAContext fireContext = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true, SourceEntity = this.transform };
        foreach (var action in skillData.OnFireActions) if (action != null) action.Execute(fireContext);

        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));
            projObj.GetComponent<Projectile>().Fire(actualTarget, finalDmg, rSkill.DummyWeapon, true, isCrit);
        }
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = actualTarget.position, PrimaryTarget = actualTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true, SourceEntity = this.transform };
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
            if (moveStateTimer <= 0) { currentMoveState = MoveExecutionState.Ready; rb.drag = 3f; }
            return;
        }

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

        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void LateUpdate() { EnforceArenaBounds(); }

    private void EnforceArenaBounds()
    {
        if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentArenaSize.x == 0) return;
        Vector2 center = CombatDirector.Instance.CurrentArenaCenter;
        Vector2 size = CombatDirector.Instance.CurrentArenaSize;

        float minX = center.x - size.x / 2f; float maxX = center.x + size.x / 2f;
        float minY = center.y - size.y / 2f; float maxY = center.y + size.y / 2f;

        Vector3 currentPos = transform.position;
        float clampedX = Mathf.Clamp(currentPos.x, minX, maxX);
        float clampedY = Mathf.Clamp(currentPos.y, minY, maxY);

        if (currentPos.x != clampedX || currentPos.y != clampedY)
        {
            transform.position = new Vector3(clampedX, clampedY, currentPos.z);
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead) return;

        if (currentMoveState == MoveExecutionState.Dashing)
        {
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();
            if (victim != null && !victim.isEnemy && !dashedVictims.Contains(victim))
            {
                dashedVictims.Add(victim);
                float myMass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);
                float kineticDamage = myMass * MyData.DashSpeedMultiplier * 5f;

                victim.TakeDamage(kineticDamage, MyData.EnemyName + " (野蛮冲撞)");

                ChimeraAIController playerAI = victim.GetComponent<ChimeraAIController>();
                if (playerAI != null)
                {
                    Vector2 knockbackDir = (victim.transform.position - transform.position).normalized;
                    playerAI.ApplyImpulse(knockbackDir, myMass * 100f);
                }

                if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(0.4f, 0.2f);
            }
        }
        else
        {
            dashedVictims.Clear();
        }
    }

    // ==========================================
    // 👇【终极死亡引擎】：关闭动画器防诈尸，纯程序接管！
    // ==========================================
    private void HandleDeathSequence()
    {
        if (isDead) return;
        isDead = true;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        rb.simulated = false;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders) col.enabled = false;

        // 【防诈尸核心】：如果挂了 Animator，强行关掉它！防止它一帧帧把 -90度旋转再给掰正！
        Animator[] anims = GetComponentsInChildren<Animator>();
        foreach (var anim in anims) anim.enabled = false;

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim != null) procAnim.StopAnimation();

        ExecuteECAActions(MyData.OnDeathActions, this.transform, null);

        gameObject.tag = "Untagged";
        gameObject.layer = LayerMask.NameToLayer("Floor");

        StartCoroutine(CorpseDecayRoutine());
    }

    private IEnumerator CorpseDecayRoutine()
    {
        float lingerTime = MyData != null ? MyData.CorpseLingerTime : 5f;
        yield return new WaitForSeconds(lingerTime);

        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        float fadeTime = 2f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            foreach (var sr in srs)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}