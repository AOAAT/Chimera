using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public enum AIState { Thinking, Positioning, Channelling, Executing }
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
    private float currentActionInterval = 0f; // 缓存由全局公式算出的秒数

    private float stateTimer = 0f;
    private float globalActionTimer = 0f;
    private float lastFrameHP;
    private bool isDead = false;
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

    private void Update()
    {
        if (isDead) return;
        if (myReceiver.CurrentHP < lastFrameHP) { ExecuteECAActions(MyData.OnTakeDamageActions, null); lastFrameHP = myReceiver.CurrentHP; }
        if (myReceiver.CurrentHP <= 0) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) { rb.velocity = Vector2.zero; return; }

        if (isStaggered) { HandleStaggerState(); return; }

        // 1. 【性能优化】：只有在 Thinking 状态才更新全局计时器
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;

        // 2. 始终更新所有技能冷却
        foreach (var s in runtimeSkills) if (s.CurrentCooldown > 0) s.CurrentCooldown -= Time.deltaTime;

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
        // --- 👇【修复 1】：将 globalActionTimer 的判定从这里移走，或设为极小值 ---
        // 之前的 0.4s 会导致哪怕技能冷却好了，大脑也在“由于 GCD 没转完而拒绝思考”
        if (runtimeSkills.Count == 0 || globalActionTimer > 0) return;

        float totalScore = 0f;
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
        foreach (var pair in candidatePool)
        {
            roll -= pair.Value;
            if (roll <= 0) { currentSkill = pair.Key; break; }
        }

        if (currentSkill != null) CurrentState = AIState.Positioning;
    }

    public void SetHUD(EntityHUD hud)
    {
        myHUD = hud;
    }
    private void ExecutePositioning()
    {
        if (currentSkill == null || currentTarget == null) { CurrentState = AIState.Thinking; return; }

        float dMult = CombatSandbox.GetDist(1f);
        float dist = CalculateDistanceToTarget(currentTarget, out Vector2 dir);
        float moveSpeed = GetFinalStat(StatType.MoveSpeed, MyData.GetStat(StatType.MoveSpeed)) * CombatSandbox.GetSpeed(1f);

        float maxR = GetFinalStat(StatType.MaxRange, currentSkill.SkillData.MaxRange) * dMult;
        float minR = GetFinalStat(StatType.MinRange, currentSkill.SkillData.MinRange) * dMult;

        if (dist > maxR) rb.velocity = dir * moveSpeed;
        else if (dist < minR) rb.velocity = -dir * moveSpeed;
        else
        {
            rb.velocity = Vector2.zero;
            if (currentSkill.SkillData.RequiresToken)
            {
                if (EnemyActionDirector.Instance != null && !EnemyActionDirector.Instance.TryRequestToken(currentSkill.SkillData.TokenType))
                {
                    currentSkill = null; CurrentState = AIState.Thinking; return;
                }
                hasActiveToken = true;
            }

            if (myHUD == null) myHUD = GetComponentInChildren<EntityHUD>();

            if (currentSkill.SkillData.ShowIntent && myHUD != null)
            {
                myHUD.ShowIntent(currentSkill.SkillData.IntentIcon, currentActionInterval);
            }
            // --- 👇【统一调用全局攻速公式】---
            float finalAtkScore = GetFinalStat(StatType.AttackSpeed, currentSkill.SkillData.AttackSpeed);
            currentActionInterval = GameFormulas.CalcCooldown(finalAtkScore); // 此时得到秒数

            if (currentSkill.SkillData.ShowIntent && myHUD != null)
                myHUD.ShowIntent(currentSkill.SkillData.IntentIcon, currentActionInterval);

            stateTimer = currentActionInterval;
            CurrentState = AIState.Channelling;
        }
        if (currentSkill.SkillData.ShowIntent)
        {
            Debug.Log($"<color=yellow>【意图尝试显示】</color> 目标HUD: {(myHUD != null ? "已连接" : "丢失")}, 技能: {currentSkill.SkillData.SkillName}");
        }
    }

    private void ExecuteChannelling()
    {
        stateTimer -= Time.deltaTime;
        rb.velocity = Vector2.zero;
        if (stateTimer <= 0)
        {
            if (myHUD != null) myHUD.HideIntent();
            PerformAttack(currentSkill);
        }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill)
    {
        CurrentState = AIState.Executing;
        var data = rSkill.SkillData;
        Transform target = data.OverrideTargeting ? GetTargetByStrategy(data.SkillTargetingLogic) : currentTarget;
        if (target == null) { FinishSkillExecution(); return; }

        // 数据同步
        float fMaxDmg = GetFinalStat(StatType.MaxDamage, data.MaxDamage);
        float fMinDmg = GetFinalStat(StatType.MinDamage, data.MinDamage);
        rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = fMaxDmg;
        rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = fMinDmg;
        rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = GetFinalStat(StatType.ProjectileSpeed, data.ProjectileSpeed);

        // --- 👇【修复 2】：这里的 globalActionTimer 不能再设为 0.4s 了 ---
        // 之前这里会强行打断连续施法的节奏。现在设为一个极小帧间距。
        globalActionTimer = 0.05f;

        Vector3 spawnPos = myHitboxCollider != null ? myHitboxCollider.bounds.center : transform.position;
        ECAContext context = new ECAContext
        {
            ImpactPoint = target.position,
            PrimaryTarget = target,
            SourceEntity = this.transform,
            IsEnemyFire = true,
            SourceWeapon = rSkill.DummyWeapon,
            BaseDamage = Random.Range(fMinDmg, fMaxDmg)
        };

        foreach (var action in data.OnFireActions) action.Execute(context);

        if (data.DeliveryType == WeaponDeliveryType.Tactical_Dash)
        {
            Vector2 attackDir = (target.position - transform.position).normalized;
            Vector2 dashDir = (data.DashDirection == TacticalDashDirection.AwayFromTarget) ? -attackDir : (data.DashDirection == TacticalDashDirection.TowardsTarget ? attackDir : new Vector2(-attackDir.y, attackDir.x));
            ApplyImpulse(dashDir, data.DashImpulse);
        }
        else if (data.DeliveryType == WeaponDeliveryType.Ranged && data.ProjectilePrefab != null)
        {
            Vector2 attackDir = (target.position - spawnPos).normalized;
            float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            GameObject proj = Instantiate(data.ProjectilePrefab, spawnPos, Quaternion.AngleAxis(angle, Vector3.forward));
            proj.GetComponent<Projectile>()?.Fire(target, context.BaseDamage, rSkill.DummyWeapon, null, this.transform, true, false, 0, false, data.ProjectilePrefab);
        }

        foreach (var action in data.OnHitActions) action.Execute(context);

        // 连招判定保持...
        if (data.NextComboSkill != null)
        {
            var next = runtimeSkills.Find(s => s.SkillData == data.NextComboSkill);
            if (next != null) { currentSkill = next; CurrentState = AIState.Positioning; return; }
        }

        FinishSkillExecution();
    }


    private void FinishSkillExecution()
    {
        if (currentSkill != null)
        {
            // 冷却结算
            currentSkill.CurrentCooldown = currentActionInterval * currentSkill.SkillData.CooldownMultiplier;

            if (hasActiveToken && EnemyActionDirector.Instance != null)
                EnemyActionDirector.Instance.ReturnToken(currentSkill.SkillData.TokenType);
        }

        // --- 👇【修复 3】：这里的 globalActionTimer 也设为极小 ---
        globalActionTimer = 0.05f;
        hasActiveToken = false; currentSkill = null; CurrentState = AIState.Thinking;
    }
    // ==========================================
    // ⚙️ 还原物理与视觉支持
    // ==========================================
    private void SetupVisuals()
    {
        if (MyData.Archetype == EnemyArchetype.Modular) { myHitboxCollider = GetComponentInChildren<BoxCollider2D>(); return; }
        GameObject vNode = transform.Find("VisualAndHitbox")?.gameObject;
        if (vNode == null) { vNode = new GameObject("VisualAndHitbox"); vNode.transform.SetParent(this.transform, false); vNode.AddComponent<SpriteRenderer>(); }
        SpriteRenderer sr = vNode.GetComponent<SpriteRenderer>();
        if (MyData.EnemySprite != null) sr.sprite = MyData.EnemySprite;
        vNode.layer = LayerMask.NameToLayer("Enemy_Hitbox");
        vNode.transform.localScale = Vector3.one * MyData.VisualScaleMultiplier;
        myHitboxCollider = vNode.GetComponent<BoxCollider2D>() ?? vNode.AddComponent<BoxCollider2D>();
        myHitboxCollider.isTrigger = true;
        if (MyData.AnimController != null) { vNode.GetComponent<Animator>().runtimeAnimatorController = MyData.AnimController; }
        ProceduralAnimator2D proc = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>();
        proc.SetTargetVisual(vNode.transform); proc.RefreshBaseState();
    }

    private void SetupPhysics()
    {
        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.drag = 3f; rb.mass = Mathf.Max(MyData.GetStat(StatType.Mass), 1f);
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 realSize = sr.sprite.bounds.size * MyData.VisualScaleMultiplier;
            BoxCollider2D phys = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>();
            phys.isTrigger = false;
            phys.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f);
            phys.offset = new Vector2(0f, -(realSize.y / 2f) + (phys.size.y / 2f));
            DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(realSize.y / 2f);
        }
        UnitFactionShadow enemyShadow = gameObject.GetComponent<UnitFactionShadow>() ?? gameObject.AddComponent<UnitFactionShadow>();
        if (sr != null)
        {
            Transform sTrans = enemyShadow.GetShadowTransform();
            sTrans.SetParent(sr.transform, false); sTrans.SetAsFirstSibling();
            if (MyData.OverrideShadow) enemyShadow.SetupManualShadow(true, MyData.ShadowWidth, MyData.ShadowHeight, MyData.ShadowOffset);
            else enemyShadow.SetupModularShadow(true, sr.bounds.size.x * MyData.VisualScaleMultiplier, -(sr.sprite.bounds.size.y * MyData.VisualScaleMultiplier / 2f));
        }
    }

    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        if (isDead) return;
        float mass = Mathf.Max(rb.mass, 0.5f);
        float deltaV = impulse / mass;
        if (deltaV < 1.0f) return;
        if (CurrentState == AIState.Channelling || CurrentState == AIState.Positioning) { if (myHUD != null) myHUD.HideIntent(); FinishSkillExecution(); }
        isStaggered = true; staggerTimer = Mathf.Max(deltaV * 0.05f, 0.1f);
        rb.drag = 5f; rb.velocity = Vector2.zero; rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void HandleStaggerState() { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 3f; CurrentState = AIState.Thinking; } }

    // --- 修改 EnemyBrain.cs 的 HandleDeathSequence 方法 ---
    private void HandleDeathSequence()
    {
        if (isDead) return;
        isDead = true;

        if (myHUD != null) myHUD.HideIntent();
        FinishSkillExecution();

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        rb.simulated = false;

        // 1. 👇【核心修复点】：在这里完整构造死亡上下文
        BuffManager bm = GetComponent<BuffManager>();
        if (bm != null)
        {
            ECAContext deathContext = new ECAContext
            {
                ImpactPoint = transform.position,
                PrimaryTarget = this.transform,
                SourceEntity = this.transform, // 👈 这一行必须加！死者就是来源
                IsEnemyFire = myReceiver.isEnemy // 👈 阵营也按实际情况传
            };

            // 触发 Buff 的死亡管线 (如烛火传染)
            bm.TriggerHolderDeathActions(deathContext);
        }

        // 2. 触发怪物的图纸死亡动作
        ExecuteECAActions(MyData.OnDeathActions, null);

        // 3. 尸体淡出
        gameObject.layer = LayerMask.NameToLayer("Floor");
        StartCoroutine(CorpseDecayRoutine());
    }

    private void InitializeSkills()
    {
        runtimeSkills.Clear();
        foreach (var skillSO in MyData.Skills)
        {
            if (skillSO == null) continue;
            var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f };
            rSkill.DummyWeapon = new RuntimeWeapon { WeaponName = skillSO.SkillName, DeliveryType = skillSO.DeliveryType, ProjectilePrefab = skillSO.ProjectilePrefab };
            rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions);
            rSkill.DummyWeapon.OnFireActions.AddRange(skillSO.OnFireActions);
            runtimeSkills.Add(rSkill);
        }
    }

    private void ExecuteECAActions(List<ECAAction> actions, RuntimeWeapon w)
    {
        if (actions == null) return;
        ECAContext c = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = this.transform, SourceWeapon = w, IsEnemyFire = true, SourceEntity = this.transform };
        foreach (var a in actions) if (a != null) a.Execute(c);
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
            foreach (var s in srs) { if (s.gameObject.name == "Logic_Visual_Shadow") continue; Color c = s.color; c.a = a; s.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }

    private float CalculateDistanceToTarget(Transform t, out Vector2 dir)
    {
        Vector2 myC = myHitboxCollider != null ? (Vector2)myHitboxCollider.bounds.center : (Vector2)transform.position;
        Collider2D tc = t.GetComponentInChildren<Collider2D>();
        if (tc != null) { Vector2 edge = tc.ClosestPoint(myC); dir = (edge - myC).normalized; return Vector2.Distance(myC, edge); }
        dir = (Vector2)(t.position - transform.position).normalized;
        return Vector2.Distance(myC, t.position);
    }

    private Transform GetTargetByStrategy(TargetingStrategy s)
    {
        var p = CombatDirector.ActivePlayerUnits.Where(r => r != null && r.CurrentHP > 0).ToList();
        if (p.Count == 0) return null;
        switch (s) { case TargetingStrategy.MaxHPHighest: return p.OrderByDescending(x => x.MaxHP).First().transform; default: return p.OrderBy(x => Vector3.Distance(transform.position, x.transform.position)).First().transform; }
    }

    private float GetFinalStat(StatType t, float baseVal = 0)
    {
        float finalVal = baseVal;
        var m = GetComponent<BuffManager>();
        if (m != null && m.BuffStatModifiers.ContainsKey(t)) finalVal += m.BuffStatModifiers[t];
        return finalVal;
    }
}