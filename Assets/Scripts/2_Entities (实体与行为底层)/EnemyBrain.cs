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

[RequireComponent(typeof(DamageReceiver), typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    public EnemyDataSO MyData;
    public enum AIState { Thinking, Positioning, Channelling, Executing, Dead }
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
    private float staggerTimer = 0f;
    private bool isStaggered = false;

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }
        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();
        myHUD = GetComponentInChildren<EntityHUD>();
        myReceiver.Initialize(Mathf.Max(MyData.GetStat(StatType.HP), 1f), MyData.GetStat(StatType.AP));
        myReceiver.isEnemy = true;
        lastFrameHP = myReceiver.CurrentHP;
        SetupVisuals(); SetupPhysics(); InitializeSkills();
        ExecuteECAActions(MyData.OnSpawnActions, null);
        myReceiver.OnEntityDeath += HandleDeathSequence;
    }

    public void SetHUD(EntityHUD hud) { myHUD = hud; }

    private void Update()
    {
        if (isDead || CurrentState == AIState.Dead) return;
        if (myReceiver.CurrentHP < lastFrameHP) { ExecuteECAActions(MyData.OnTakeDamageActions, null); CheckInterrupt(); lastFrameHP = myReceiver.CurrentHP; }
        if (myReceiver.CurrentHP <= 0) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) { rb.velocity = Vector2.zero; return; }
        if (isStaggered) { HandleStaggerState(); return; }
        foreach (var s in runtimeSkills) if (s.CurrentCooldown > 0) s.CurrentCooldown -= Time.deltaTime;
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;
        currentTarget = GetTargetByStrategy(MyData.TargetingLogic);
        HandleTacticalStateMachine();
    }

    private void HandleTacticalStateMachine()
    {
        if (currentTarget == null && (currentSkill == null || !currentSkill.SkillData.IgnoreRange))
        { rb.velocity = Vector2.zero; if (CurrentState != AIState.Executing) CurrentState = AIState.Thinking; return; }
        switch (CurrentState) { case AIState.Thinking: DecideNextIntent(); break; case AIState.Positioning: ExecutePositioning(); break; case AIState.Channelling: ExecuteChannelling(); break; }
    }

    private void DecideNextIntent()
    {
        if (globalActionTimer > 0 || runtimeSkills.Count == 0) return;
        List<KeyValuePair<RuntimeEnemySkill, float>> candidatePool = new List<KeyValuePair<RuntimeEnemySkill, float>>();
        foreach (var s in runtimeSkills)
        {
            if (s.CurrentCooldown > 0) continue;
            float score = s.SkillData.BaseScore;
            if (s.SkillData.Evaluators != null) foreach (var eval in s.SkillData.Evaluators) if (eval != null) score += eval.CalculateScore(this, s.SkillData, currentTarget);
            if (score > 0) candidatePool.Add(new KeyValuePair<RuntimeEnemySkill, float>(s, score));
        }
        if (candidatePool.Count == 0) return;
        float roll = Random.Range(0, candidatePool.Sum(x => x.Value));
        foreach (var pair in candidatePool) { roll -= pair.Value; if (roll <= 0) { currentSkill = pair.Key; break; } }
        if (currentSkill != null) CurrentState = AIState.Positioning;
    }

    private void ExecutePositioning()
    {
        if (currentSkill == null) { CurrentState = AIState.Thinking; return; }
        if (currentSkill.SkillData.IgnoreRange) { EnterChannellingPhase(); return; }
        if (currentTarget == null) { CurrentState = AIState.Thinking; return; }
        float dMult = CombatSandbox.GetDist(1f), sMult = CombatSandbox.GetSpeed(1f);
        float dist = CalculateDistanceToTarget(currentTarget, out Vector2 dir);
        float moveSpeed = GetFinalStat(StatType.MoveSpeed, MyData.GetStat(StatType.MoveSpeed)) * sMult;
        float maxR = GetFinalStat(StatType.MaxRange, currentSkill.SkillData.MaxRange) * dMult;
        float minR = GetFinalStat(StatType.MinRange, currentSkill.SkillData.MinRange) * dMult;
        if (dist > maxR) rb.velocity = dir * moveSpeed; else if (dist < minR) rb.velocity = -dir * moveSpeed; else { rb.velocity = Vector2.zero; EnterChannellingPhase(); }
    }

    private void EnterChannellingPhase()
    {
        if (currentSkill.SkillData.RequiresToken)
        {
            if (EnemyActionDirector.Instance != null && !EnemyActionDirector.Instance.TryRequestToken(currentSkill.SkillData.TokenType))
            { currentSkill = null; globalActionTimer = 0.2f; CurrentState = AIState.Thinking; return; }
            hasActiveToken = true;
        }
        currentActionInterval = GameFormulas.CalcCooldown(GetFinalStat(StatType.AttackSpeed, currentSkill.SkillData.AttackSpeed));
        if (currentSkill.SkillData.ShowIntent && myHUD != null) myHUD.ShowIntent(currentSkill.SkillData.IntentIcon, currentActionInterval);
        stateTimer = currentActionInterval; CurrentState = AIState.Channelling;
    }

    private void ExecuteChannelling()
    {
        stateTimer -= Time.deltaTime; rb.velocity = Vector2.zero;
        if (stateTimer <= 0) { if (myHUD != null) myHUD.HideIntent(); PerformAttack(currentSkill); }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill)
    {
        CurrentState = AIState.Executing;
        var data = rSkill.SkillData;
        Transform target = data.OverrideTargeting ? GetTargetByStrategy(data.SkillTargetingLogic) : currentTarget;
        rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = GetFinalStat(StatType.MaxDamage, data.MaxDamage);
        rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = GetFinalStat(StatType.MinDamage, data.MinDamage);
        rSkill.DummyWeapon.WeaponStats[StatType.MaxRange] = GetFinalStat(StatType.MaxRange, data.MaxRange);
        rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = GetFinalStat(StatType.ProjectileSpeed, data.ProjectileSpeed) * CombatSandbox.GetSpeed(1f);

        ECAContext context = new ECAContext
        {
            ImpactPoint = target != null ? target.position : transform.position + transform.right,
            PrimaryTarget = target,
            SourceEntity = this.transform,
            IsEnemyFire = true,
            SourceWeapon = rSkill.DummyWeapon,
            BaseDamage = Random.Range(rSkill.DummyWeapon.GetStat(StatType.MinDamage), rSkill.DummyWeapon.GetStat(StatType.MaxDamage))
        };

        foreach (var action in data.OnFireActions) if (action != null) action.Execute(context);

        // 👇【核心对齐】：使用原有的 WeaponDeliveryType 逻辑名
        if (data.DeliveryType == WeaponDeliveryType.Special) { }
        else if (data.DeliveryType == WeaponDeliveryType.Melee && target != null)
        {
            foreach (var action in data.OnHitActions) action.Execute(context);
        }
        else if (data.DeliveryType == WeaponDeliveryType.Ranged && target != null && data.ProjectilePrefab != null)
        {
            FireProjectile(target, context, rSkill);
        }

        if (data.NextComboSkill != null)
        {
            var next = runtimeSkills.Find(s => s.SkillData == data.NextComboSkill);
            if (next != null) { currentSkill = next; CurrentState = AIState.Positioning; return; }
        }
        FinishSkillExecution();
    }

    private void FireProjectile(Transform target, ECAContext ctx, RuntimeEnemySkill rSkill)
    {
        Vector3 spawnPos = myHitboxCollider != null ? myHitboxCollider.bounds.center : transform.position;
        Vector2 attackDir = (target.position - spawnPos).normalized;
        GameObject proj = Instantiate(rSkill.SkillData.ProjectilePrefab, spawnPos, Quaternion.AngleAxis(Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg, Vector3.forward));
        Projectile p = proj.GetComponent<Projectile>();
        if (p != null) p.Fire(target, ctx.BaseDamage, rSkill.DummyWeapon, null, this.transform, true, false, 0, false, rSkill.SkillData.ProjectilePrefab);
    }

    private void CheckInterrupt()
    {
        var handler = GetComponent<KineticCollisionHandler>();
        if (handler != null && handler.IsUnstoppable) return;
        if (CurrentState == AIState.Channelling || CurrentState == AIState.Positioning)
        {
            // 1. 寻找身上是否正在引导激光
            var activeLaser = GetComponentInChildren<LinearLaserController>();

            // --- 👇【关键修复】：增加霸体判定 ---
            if (activeLaser != null && activeLaser.IsUnstoppable)
            {
                Debug.Log($"<color=white>【稳态】</color> {gameObject.name} 依靠霸体维持了激光引导！");
                return; // 霸体生效，不执行下方的销毁和重置逻辑
            }
            // ------------------------------------

            // 2. 如果没霸体，或者是其他可打断技能，则执行强制中止
            if (activeLaser != null) Destroy(activeLaser.gameObject);

            if (myHUD != null) myHUD.HideIntent();
            Debug.Log($"<color=yellow>【打断】</color> {gameObject.name} 的引导因冲击而中止");
            FinishSkillExecution();
        }
    }

    private void FinishSkillExecution()
    {
        if (currentSkill != null)
        {
            currentSkill.CurrentCooldown = currentActionInterval * currentSkill.SkillData.CooldownMultiplier;
            if (hasActiveToken && EnemyActionDirector.Instance != null) EnemyActionDirector.Instance.ReturnToken(currentSkill.SkillData.TokenType);
        }
        hasActiveToken = false; currentSkill = null; globalActionTimer = 0.1f; CurrentState = AIState.Thinking;
    }

    private void HandleStaggerState() { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 3f; CurrentState = AIState.Thinking; } }
    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        if (isDead) return;

        // 同时兼容激光霸体和冲撞霸体
        var laser = GetComponentInChildren<LinearLaserController>();
        var kinetic = GetComponent<KineticCollisionHandler>();

        bool hasSuperArmor = (laser != null && laser.IsUnstoppable) || (kinetic != null && kinetic.IsUnstoppable);

        if (hasSuperArmor)
        {
            return; // 不产生硬直，不产生位移
        }

        float mass = Mathf.Max(rb.mass, 0.5f);
        float deltaV = impulse / mass;
        if (deltaV < 1.0f) return;

        CheckInterrupt();
        isStaggered = true;
        staggerTimer = Mathf.Max(deltaV * 0.05f, 0.1f);
        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void HandleDeathSequence() { if (isDead) return; isDead = true; CurrentState = AIState.Dead; if (myHUD != null) myHUD.HideIntent(); FinishSkillExecution(); rb.velocity = Vector2.zero; rb.isKinematic = true; rb.simulated = false; ExecuteECAActions(MyData.OnDeathActions, null); gameObject.layer = LayerMask.NameToLayer("Floor"); StartCoroutine(CorpseDecayRoutine()); }
    private void InitializeSkills() { runtimeSkills.Clear(); foreach (var skillSO in MyData.Skills) { if (skillSO == null) continue; var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f }; rSkill.DummyWeapon = new RuntimeWeapon { WeaponName = skillSO.SkillName, DeliveryType = skillSO.DeliveryType, ProjectilePrefab = skillSO.ProjectilePrefab }; rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed; rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage; rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage; rSkill.DummyWeapon.WeaponStats[StatType.MaxRange] = skillSO.MaxRange; rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed; rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions); rSkill.DummyWeapon.OnFireActions.AddRange(skillSO.OnFireActions); runtimeSkills.Add(rSkill); } }
    private void ExecuteECAActions(List<ECAAction> actions, RuntimeWeapon w) { if (actions == null) return; ECAContext c = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = this.transform, SourceWeapon = w, IsEnemyFire = true, SourceEntity = this.transform }; foreach (var a in actions) if (a != null) a.Execute(c); }
    private IEnumerator CorpseDecayRoutine() { yield return new WaitForSeconds(MyData.CorpseLingerTime); float f = 2f, e = 0f; var srs = GetComponentsInChildren<SpriteRenderer>(); while (e < f) { e += Time.deltaTime; float a = Mathf.Lerp(1f, 0f, e / f); foreach (var s in srs) { if (s.gameObject.name == "Logic_Visual_Shadow") continue; Color c = s.color; c.a = a; s.color = c; } yield return null; } Destroy(gameObject); }
    private float CalculateDistanceToTarget(Transform t, out Vector2 dir) { if (myHitboxCollider == null || !myHitboxCollider.enabled) { dir = (Vector2)(t.position - transform.position).normalized; return Vector2.Distance(transform.position, t.position); } Vector2 myC = (Vector2)myHitboxCollider.bounds.center; Collider2D tc = t.GetComponentInChildren<Collider2D>(); if (tc != null) { Vector2 edge = tc.ClosestPoint(myC); dir = (edge - myC).normalized; if (dir == Vector2.zero) dir = (Vector2)(t.position - transform.position).normalized; return Vector2.Distance(myC, edge); } dir = (Vector2)(t.position - transform.position).normalized; return Vector2.Distance(myC, t.position); }
    private Transform GetTargetByStrategy(TargetingStrategy s) { var p = CombatDirector.ActivePlayerUnits.Where(r => r != null && r.CurrentHP > 0).ToList(); if (p.Count == 0) return null; switch (s) { case TargetingStrategy.MaxHPHighest: return p.OrderByDescending(x => x.MaxHP).First().transform; default: return p.OrderBy(x => Vector3.Distance(transform.position, x.transform.position)).First().transform; } }
    private float GetFinalStat(StatType t, float baseVal = 0)
    {
        var m = GetComponent<BuffManager>();
        if (m != null)
        {
            // 👇【核心系统调用】
            return m.GetAdjustedStat(t, baseVal);
        }
        return baseVal;
    }

    private void SetupVisuals() { if (MyData.Archetype == EnemyArchetype.Modular) { myHitboxCollider = GetComponentInChildren<BoxCollider2D>(); return; } GameObject vNode = transform.Find("VisualAndHitbox")?.gameObject; if (vNode == null) { vNode = new GameObject("VisualAndHitbox"); vNode.transform.SetParent(this.transform, false); vNode.AddComponent<SpriteRenderer>(); } SpriteRenderer sr = vNode.GetComponent<SpriteRenderer>(); if (MyData.EnemySprite != null) sr.sprite = MyData.EnemySprite; vNode.layer = LayerMask.NameToLayer("Enemy_Hitbox"); vNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier; myHitboxCollider = vNode.GetComponent<BoxCollider2D>() ?? vNode.AddComponent<BoxCollider2D>(); myHitboxCollider.isTrigger = true; if (MyData.AnimController != null) vNode.GetComponent<Animator>().runtimeAnimatorController = MyData.AnimController; ProceduralAnimator2D proc = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>(); proc.SetTargetVisual(vNode.transform); proc.RefreshBaseState(); }
    private void SetupPhysics() { gameObject.layer = LayerMask.NameToLayer("Enemy_Body"); rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.drag = 3f; rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f); SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(); if (sr != null && sr.sprite != null) { Vector2 realSize = sr.sprite.bounds.size * MyData.VisualScaleMultiplier; BoxCollider2D phys = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>(); phys.isTrigger = false; phys.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f); phys.offset = new Vector2(0f, -(realSize.y / 2f) + (phys.size.y / 2f)); DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>(); sorter.YOffset = -(realSize.y / 2f); } UnitFactionShadow shadow = GetComponent<UnitFactionShadow>() ?? gameObject.AddComponent<UnitFactionShadow>(); if (sr != null) { Transform sTrans = shadow.GetShadowTransform(); sTrans.SetParent(sr.transform, false); sTrans.SetAsFirstSibling(); if (MyData.OverrideShadow) shadow.SetupManualShadow(true, MyData.ShadowWidth, MyData.ShadowHeight, MyData.ShadowOffset); else shadow.SetupModularShadow(true, sr.bounds.size.x * MyData.VisualScaleMultiplier, -(sr.sprite.bounds.size.y * MyData.VisualScaleMultiplier / 2f)); } }
}