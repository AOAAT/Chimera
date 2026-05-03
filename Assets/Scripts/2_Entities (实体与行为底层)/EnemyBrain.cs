using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ==========================================
// 1. 运行时怪物技能实例
// ==========================================
public class RuntimeEnemySkill
{
    public EnemySkillSO SkillData;
    public float CurrentCooldown;
    public RuntimeWeapon DummyWeapon;
    public float LastCalculatedScore;
}

[RequireComponent(typeof(DamageReceiver), typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    public EnemyDataSO MyData;

    public enum AIState { Thinking, Positioning, Channelling, Executing, Dead }
    [Header("=== 运行时 AI 状态 ===")]
    public AIState CurrentState = AIState.Thinking;

    private DamageReceiver myReceiver;
    private Rigidbody2D rb;
    private Collider2D myHitboxCollider;
    private EntityHUD myHUD;

    private Transform currentTarget;
    private List<RuntimeEnemySkill> runtimeSkills = new List<RuntimeEnemySkill>();

    private RuntimeEnemySkill currentSkill = null;
    private bool hasActiveToken = false;
    private float currentActionInterval = 0f;

    private float stateTimer = 0f;
    private float globalActionTimer = 0f;
    private float lastFrameHP;
    private bool isDead = false;

    // --- 📥 持续位移控制 ---
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector2 dashVelocity;

    // --- 物理硬直状态 ---
    private bool isStaggered = false;
    private float staggerTimer = 0f;

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }
        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();
        myHUD = GetComponentInChildren<EntityHUD>();

        myReceiver.Initialize(Mathf.Max(MyData.GetStat(StatType.HP), 1f), MyData.GetStat(StatType.AP));
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;

        SetupVisuals();
        SetupPhysics();
        InitializeSkills();
        ExecuteECAActions(MyData.OnSpawnActions, null);
        myReceiver.OnEntityDeath += HandleDeathSequence;
    }

    public void SetHUD(EntityHUD hud) { myHUD = hud; }

    private void Update()
    {
        if (isDead || CurrentState == AIState.Dead) return;

        // 受击处理
        if (myReceiver.CurrentHP < lastFrameHP) { ExecuteECAActions(MyData.OnTakeDamageActions, null); lastFrameHP = myReceiver.CurrentHP; }
        if (myReceiver.CurrentHP <= 0) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) { rb.velocity = Vector2.zero; return; }

        // 1. 【高优先级】：持续位移（冲刺）接管
        if (isDashing) { HandleDashingState(); return; }

        // 2. 【高优先级】：物理硬直接管
        if (isStaggered) { HandleStaggerState(); return; }

        // 冷却与思考计时
        foreach (var s in runtimeSkills) if (s.CurrentCooldown > 0) s.CurrentCooldown -= Time.deltaTime;
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;

        currentTarget = GetTargetByStrategy(MyData.TargetingLogic);
        HandleTacticalStateMachine();
    }

    private void HandleTacticalStateMachine()
    {
        if (currentTarget == null) { rb.velocity = Vector2.zero; return; }
        switch (CurrentState)
        {
            case AIState.Thinking: DecideNextIntent(); break;
            case AIState.Positioning: ExecutePositioning(); break;
            case AIState.Channelling: ExecuteChannelling(); break;
            case AIState.Executing: break;
        }
    }

    private void DecideNextIntent()
    {
        if (globalActionTimer > 0 || runtimeSkills.Count == 0) return;
        RuntimeEnemySkill selected = null; float totalScore = 0f;
        List<KeyValuePair<RuntimeEnemySkill, float>> candidatePool = new List<KeyValuePair<RuntimeEnemySkill, float>>();

        foreach (var s in runtimeSkills)
        {
            if (s.CurrentCooldown > 0) continue;
            float score = s.SkillData.BaseScore;
            if (s.SkillData.Evaluators != null)
                foreach (var eval in s.SkillData.Evaluators) if (eval != null) score += eval.CalculateScore(this, s.SkillData, currentTarget);
            score = Mathf.Max(0, score);
            s.LastCalculatedScore = score;
            if (score > 0) { candidatePool.Add(new KeyValuePair<RuntimeEnemySkill, float>(s, score)); totalScore += score; }
        }

        if (candidatePool.Count == 0) return;
        float roll = Random.Range(0, totalScore);
        foreach (var pair in candidatePool) { roll -= pair.Value; if (roll <= 0) { selected = pair.Key; break; } }
        if (selected != null) { currentSkill = selected; CurrentState = AIState.Positioning; }
    }
    private void ExecutePositioning()
    {
        if (currentSkill == null || currentTarget == null) { CurrentState = AIState.Thinking; return; }

        // 1. 统一调用度量衡缩放
        float dMult = CombatSandbox.GetDist(1f);
        float sMult = CombatSandbox.GetSpeed(1f);
        float dist = CalculateDistanceToTarget(currentTarget, out Vector2 dir);
        float moveSpeed = GetFinalStat(StatType.MoveSpeed, MyData.GetStat(StatType.MoveSpeed)) * sMult;

        // --- 👇【核心修复 A】：全域技能处理 ---
        if (currentSkill.SkillData.IgnoreRange)
        {
            // 如果是全域技能，不干扰基础位移，怪物继续按照底盘逻辑（蜂拥/炮台）移动
            ApplyBaseMovement(dist, dir, moveSpeed, dMult);

            // 但逻辑上，它已经完成了“定位”，直接进入申请令牌和蓄力流程
            EnterChannellingPhase();
            return;
        }

        // 2. 常规射程判定 (已应用度量衡)
        float maxR = GetFinalStat(StatType.MaxRange, currentSkill.SkillData.MaxRange) * dMult;
        float minR = GetFinalStat(StatType.MinRange, currentSkill.SkillData.MinRange) * dMult;

        if (dist > maxR)
        {
            rb.velocity = dir * moveSpeed;
        }
        else if (dist < minR)
        {
            rb.velocity = -dir * moveSpeed;
        }
        else
        {
            // 成功进入特定射程区间
            rb.velocity = Vector2.zero;
            EnterChannellingPhase();
        }
    }
    private void EnterChannellingPhase()
    {
        // 令牌申请
        if (currentSkill.SkillData.RequiresToken)
        {
            if (EnemyActionDirector.Instance != null && !EnemyActionDirector.Instance.TryRequestToken(currentSkill.SkillData.TokenType))
            {
                currentSkill = null;
                globalActionTimer = 0.2f;
                CurrentState = AIState.Thinking;
                return;
            }
            hasActiveToken = true;
        }

        // 攻速计算
        currentActionInterval = GameFormulas.CalcCooldown(GetFinalStat(StatType.AttackSpeed, currentSkill.SkillData.AttackSpeed));
        if (currentSkill.SkillData.ShowIntent && myHUD != null)
            myHUD.ShowIntent(currentSkill.SkillData.IntentIcon, currentActionInterval);

        stateTimer = currentActionInterval;
        CurrentState = AIState.Channelling;
    }

    // 辅助方法：基础位移逻辑（供全域技能使用，防止原地罚站）
    private void ApplyBaseMovement(float dist, Vector2 dir, float speed, float dMult)
    {
        if (MyData.MovementLogic == EnemyMovementStrategy.Swarm)
        {
            if (dist > MyData.StopDistance * dMult) rb.velocity = dir * speed;
            else rb.velocity = Vector2.zero;
        }
        else if (MyData.MovementLogic == EnemyMovementStrategy.Artillery)
        {
            float hDist = MyData.HoverDistance * dMult;
            if (dist > hDist + 0.5f) rb.velocity = dir * speed;
            else if (dist < hDist - 0.5f) rb.velocity = -dir * speed;
            else rb.velocity = Vector2.zero;
        }
    }

    private void ExecuteChannelling()
    {
        stateTimer -= Time.deltaTime;
        rb.velocity = Vector2.zero;
        if (stateTimer <= 0) { if (myHUD != null) myHUD.HideIntent(); PerformAttack(currentSkill); }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill)
    {
        CurrentState = AIState.Executing;
        var data = rSkill.SkillData;

        // 1. 确定最终目标 (独立索敌 vs 默认目标)
        Transform target = data.OverrideTargeting ? GetTargetByStrategy(data.SkillTargetingLogic) : currentTarget;

        if (target == null)
        {
            FinishSkillExecution();
            return;
        }

        // 2. 数值同步：将受 Buff 影响的最终数值注入 DummyWeapon 字典
        float fMaxDmg = GetFinalStat(StatType.MaxDamage, data.MaxDamage);
        float fMinDmg = GetFinalStat(StatType.MinDamage, data.MinDamage);
        float fProjSpd = GetFinalStat(StatType.ProjectileSpeed, data.ProjectileSpeed); // 👈 对齐度量衡
        float fMaxRange = GetFinalStat(StatType.MaxRange, data.MaxRange);
        rSkill.DummyWeapon.WeaponStats[StatType.MaxRange] = fMaxRange;
        rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = fMaxDmg;
        rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = fMinDmg;
        rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = fProjSpd;

        // 攻速也同步一下，防止 ECA 积木需要查询
        rSkill.DummyWeapon.WeaponStats[StatType.MaxRange] = GetFinalStat(StatType.MaxRange, rSkill.SkillData.MaxRange);
        // 3. 确定发射点 (视觉中心)
        Vector3 spawnPos = myHitboxCollider != null ? myHitboxCollider.bounds.center : transform.position;

        // 4. 构造 ECA 上下文
        ECAContext context = new ECAContext
        {
            ImpactPoint = target.position,
            PrimaryTarget = target,
            SourceEntity = this.transform,
            IsEnemyFire = true,
            SourceWeapon = rSkill.DummyWeapon,
            BaseDamage = Random.Range(fMinDmg, fMaxDmg)
        };

        // 5. 触发开火管线 (表现、消耗等)
        foreach (var action in data.OnFireActions)
        {
            if (action != null) action.Execute(context);
        }

        // 6. 根据投递方式执行核心逻辑
        if (data.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            // --- 持续位移逻辑 ---
            Vector2 attackDir = (target.position - transform.position).normalized;
            Vector2 dir = (data.DashDirection == TacticalDashDirection.AwayFromTarget) ? -attackDir :
                         (data.DashDirection == TacticalDashDirection.TowardsTarget ? attackDir : new Vector2(-attackDir.y, attackDir.x));

            isDashing = true;
            dashTimer = data.DashDuration; // 使用图纸配置的持续时间

            // 将 DashImpulse 作为速度系数 (基于当前移动速度的加成)
            float baseRefSpeed = GetFinalStat(StatType.MoveSpeed, MyData.GetStat(StatType.MoveSpeed)) * CombatSandbox.GetSpeed(1f);
            float dashSpeed = (data.DashImpulse / 100f) * baseRefSpeed;
            dashVelocity = dir * dashSpeed;

            // 位移通常伴随即时判定
            TriggerHitPipeline(context, data);
        }
        else if (data.DeliveryType == WeaponDeliveryType.Melee)
        {
            // --- 近战逻辑：直接触发命中管线 ---
            TriggerHitPipeline(context, data);
        }
        else if (data.DeliveryType == WeaponDeliveryType.Ranged && data.ProjectilePrefab != null)
        {
            // --- 远程逻辑：实例化并赋予受度量衡修正的速度 ---
            Vector2 attackDir = (target.position - spawnPos).normalized;
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;

            GameObject proj = Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.AngleAxis(angle, Vector3.forward));
            Projectile pScript = proj.GetComponent<Projectile>();

            if (pScript != null)
            {
                // 子弹 Fire 内部会读取 rSkill.DummyWeapon 里我们刚刚同步好的 fProjSpd
                pScript.Fire(
                    target,
                    context.BaseDamage,
                    rSkill.DummyWeapon,
                    null,
                    this.transform,
                    true,
                    false,
                    0,
                    false,
                    data.ProjectilePrefab
                );
            }
        }

        // 7. 连招检查 (如果存在连招，直接跳过 FinishSkillExecution 的清理阶段)
        if (data.NextComboSkill != null)
        {
            var next = runtimeSkills.Find(s => s.SkillData == data.NextComboSkill);
            if (next != null)
            {
                currentSkill = next;
                CurrentState = AIState.Positioning;
                return;
            }
        }

        // 8. 正常结束技能，进入冷却
        FinishSkillExecution();
    }

    private void TriggerHitPipeline(ECAContext context, EnemySkillSO data)
    {
        foreach (var action in data.OnHitActions) action.Execute(context);
    }

    private void HandleDashingState()
    {
        dashTimer -= Time.deltaTime;
        rb.velocity = dashVelocity;
        if (dashTimer <= 0)
        {
            isDashing = false;
            rb.velocity = Vector2.zero;
            if (CurrentState == AIState.Executing) FinishSkillExecution();
        }
    }

    private void FinishSkillExecution()
    {
        if (currentSkill != null)
        {
            currentSkill.CurrentCooldown = currentActionInterval * currentSkill.SkillData.CooldownMultiplier;
            if (hasActiveToken && EnemyActionDirector.Instance != null)
                EnemyActionDirector.Instance.ReturnToken(currentSkill.SkillData.TokenType);
        }
        hasActiveToken = false; currentSkill = null; globalActionTimer = 0.1f; CurrentState = AIState.Thinking;
    }

    // ==========================================
    // ⚙️ 还原主程物理/视觉支持系统 (未做变动)
    // ==========================================
    private void SetupVisuals() { if (MyData.Archetype == EnemyArchetype.Modular) { myHitboxCollider = GetComponentInChildren<BoxCollider2D>(); return; } GameObject vNode = transform.Find("VisualAndHitbox")?.gameObject; if (vNode == null) { vNode = new GameObject("VisualAndHitbox"); vNode.transform.SetParent(this.transform, false); vNode.AddComponent<SpriteRenderer>(); } SpriteRenderer sr = vNode.GetComponent<SpriteRenderer>(); if (MyData.EnemySprite != null) sr.sprite = MyData.EnemySprite; vNode.layer = LayerMask.NameToLayer("Enemy_Hitbox"); vNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier; myHitboxCollider = vNode.GetComponent<BoxCollider2D>() ?? vNode.AddComponent<BoxCollider2D>(); myHitboxCollider.isTrigger = true; if (MyData.AnimController != null) vNode.GetComponent<Animator>().runtimeAnimatorController = MyData.AnimController; ProceduralAnimator2D proc = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>(); proc.SetTargetVisual(vNode.transform); proc.RefreshBaseState(); }
    private void SetupPhysics() { gameObject.layer = LayerMask.NameToLayer("Enemy_Body"); rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.drag = 3f; rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f); SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(); if (sr != null && sr.sprite != null) { Vector2 realSize = sr.sprite.bounds.size * MyData.VisualScaleMultiplier; BoxCollider2D phys = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>(); phys.isTrigger = false; phys.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f); phys.offset = new Vector2(0f, -(realSize.y / 2f) + (phys.size.y / 2f)); DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>(); sorter.YOffset = -(realSize.y / 2f); } UnitFactionShadow shadow = GetComponent<UnitFactionShadow>() ?? gameObject.AddComponent<UnitFactionShadow>(); if (sr != null) { Transform sTrans = shadow.GetShadowTransform(); sTrans.SetParent(sr.transform, false); sTrans.SetAsFirstSibling(); if (MyData.OverrideShadow) shadow.SetupManualShadow(true, MyData.ShadowWidth, MyData.ShadowHeight, MyData.ShadowOffset); else shadow.SetupModularShadow(true, sr.bounds.size.x * MyData.VisualScaleMultiplier, -(sr.sprite.bounds.size.y * MyData.VisualScaleMultiplier / 2f)); } }
    private void HandleStaggerState() { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 3f; CurrentState = AIState.Thinking; } }
    public void ApplyImpulse(Vector2 dir, float impulse) { if (isDead) return; float mass = Mathf.Max(rb.mass, 0.5f); float deltaV = impulse / mass; if (deltaV < 1.0f) return; isDashing = false; if (CurrentState == AIState.Channelling || CurrentState == AIState.Positioning) { if (myHUD != null) myHUD.HideIntent(); FinishSkillExecution(); } isStaggered = true; staggerTimer = Mathf.Max(deltaV * 0.05f, 0.1f); rb.drag = 5f; rb.velocity = Vector2.zero; rb.AddForce(dir * impulse, ForceMode2D.Impulse); }
    private void HandleDeathSequence() { if (isDead) return; isDead = true; CurrentState = AIState.Dead; isDashing = false; if (myHUD != null) myHUD.HideIntent(); FinishSkillExecution(); rb.velocity = Vector2.zero; rb.isKinematic = true; rb.simulated = false; BuffManager bm = GetComponent<BuffManager>(); if (bm != null) bm.TriggerHolderDeathActions(new ECAContext { ImpactPoint = transform.position, PrimaryTarget = this.transform, IsEnemyFire = true, SourceEntity = this.transform }); ExecuteECAActions(MyData.OnDeathActions, null); gameObject.layer = LayerMask.NameToLayer("Floor"); StartCoroutine(CorpseDecayRoutine()); }
    private void InitializeSkills() { runtimeSkills.Clear(); foreach (var skillSO in MyData.Skills) { if (skillSO == null) continue; var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f }; rSkill.DummyWeapon = new RuntimeWeapon { WeaponName = skillSO.SkillName, DeliveryType = skillSO.DeliveryType, ProjectilePrefab = skillSO.ProjectilePrefab }; rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed; rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage; rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage; rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed; rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions); rSkill.DummyWeapon.OnFireActions.AddRange(skillSO.OnFireActions); runtimeSkills.Add(rSkill); } }
    private void ExecuteECAActions(List<ECAAction> actions, RuntimeWeapon w) { if (actions == null) return; ECAContext c = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = this.transform, SourceWeapon = w, IsEnemyFire = true, SourceEntity = this.transform }; foreach (var a in actions) if (a != null) a.Execute(c); }
    private IEnumerator CorpseDecayRoutine() { yield return new WaitForSeconds(MyData.CorpseLingerTime); float f = 2f, e = 0f; var srs = GetComponentsInChildren<SpriteRenderer>(); while (e < f) { e += Time.deltaTime; float a = Mathf.Lerp(1f, 0f, e / f); foreach (var s in srs) { if (s.gameObject.name == "Logic_Visual_Shadow") continue; Color c = s.color; c.a = a; s.color = c; } yield return null; } Destroy(gameObject); }
    private float CalculateDistanceToTarget(Transform t, out Vector2 dir)
    {
        // 如果 Collider 被禁用了（处于虚空维度），直接用 transform.position 计算，不再查 bounds
        if (myHitboxCollider == null || !myHitboxCollider.enabled)
        {
            dir = (Vector2)(t.position - transform.position).normalized;
            return Vector2.Distance(transform.position, t.position);
        }

        Vector2 myC = (Vector2)myHitboxCollider.bounds.center;
        Collider2D tc = t.GetComponentInChildren<Collider2D>();
        if (tc != null)
        {
            Vector2 edge = tc.ClosestPoint(myC);
            dir = (edge - myC).normalized;
            if (dir == Vector2.zero) dir = (Vector2)(t.position - transform.position).normalized;
            return Vector2.Distance(myC, edge);
        }
        dir = (Vector2)(t.position - transform.position).normalized;
        return Vector2.Distance(myC, t.position);
    }
    private Transform GetTargetByStrategy(TargetingStrategy s) { var p = CombatDirector.ActivePlayerUnits.Where(r => r != null && r.CurrentHP > 0).ToList(); if (p.Count == 0) return null; switch (s) { case TargetingStrategy.MaxHPHighest: return p.OrderByDescending(x => x.MaxHP).First().transform; default: return p.OrderBy(x => Vector3.Distance(transform.position, x.transform.position)).First().transform; } }
    private float GetFinalStat(StatType t, float baseVal = 0) { float finalVal = baseVal; var m = GetComponent<BuffManager>(); if (m != null && m.BuffStatModifiers.ContainsKey(t)) finalVal += m.BuffStatModifiers[t]; return finalVal; }
}