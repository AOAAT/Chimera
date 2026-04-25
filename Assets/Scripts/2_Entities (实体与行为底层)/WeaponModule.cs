using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private enum WeaponState { Idle, Windup, Strike, Recovery }
    private WeaponState currentState = WeaponState.Idle;

    private RuntimeWeapon weaponData;
    private RuntimeChimeraData ownerData;

    private float totalCooldown = 0f;
    private float stateTimer = 0f;
    private float t_Windup, t_Strike, t_Recovery;
    private Quaternion rot_Base, rot_Windup, rot_Strike;
    private float aimAngle;
    private Vector2 logicCenterOffset;
    private Transform mechRoot;
    private Transform actualHinge;
    private Transform muzzlePoint;
    private Animator myAnimator;

    private Transform lockedTarget;
    private List<Transform> currentMultiTargets = new List<Transform>();
    private float scanTimer = 0f;
    private const float SCAN_INTERVAL = 0.3f;
    private bool cachedIsEnemy;
    private DamageReceiver mechReceiver;

    public void Initialize(RuntimeWeapon data, RuntimeChimeraData owner, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        ownerData = owner;
        logicCenterOffset = centerOffset;
        mechRoot = root;
        actualHinge = GetActualHinge();
        if (actualHinge.childCount > 0) myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);

        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset * distMult;
        muzzlePoint = muzzleObj.transform;

        currentState = WeaponState.Idle;
        stateTimer = 0f;
        mechReceiver = mechRoot.GetComponent<DamageReceiver>();
        cachedIsEnemy = mechReceiver != null ? mechReceiver.isEnemy : false;
    }

    private void Update()
    {
        if (weaponData == null || actualHinge == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        UpdateTargetSelection();

        if (lockedTarget != null)
        {
            Vector3 aimDir = lockedTarget.position - actualHinge.position;
            aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            actualHinge.rotation = Quaternion.RotateTowards(actualHinge.rotation, Quaternion.AngleAxis(aimAngle, Vector3.forward), 720f * Time.deltaTime);

            if (currentState == WeaponState.Idle && stateTimer <= 0f)
            {
                if (IsTargetInRange(lockedTarget)) InitiateAttack(lockedTarget);
            }
        }

        if (stateTimer > 0f) stateTimer -= Time.deltaTime;
        if (scanTimer > 0f) scanTimer -= Time.deltaTime;

        HandleStateTransitions();
    }

    private void HandleStateTransitions()
    {
        if (stateTimer <= 0f)
        {
            switch (currentState)
            {
                case WeaponState.Windup: currentState = WeaponState.Strike; stateTimer = t_Strike; break;
                case WeaponState.Strike: FirePayload(); currentState = WeaponState.Recovery; stateTimer = t_Recovery; break;
                case WeaponState.Recovery: currentState = WeaponState.Idle; stateTimer = 0f; break;
            }
        }

        if (weaponData.DeliveryType == WeaponDeliveryType.Melee && currentState != WeaponState.Idle)
            UpdateMeleeAnimation();
    }

    // --- 替换 WeaponModule.cs 中的 UpdateTargetSelection 相关逻辑 ---

    private Collider2D[] results = new Collider2D[20]; // 预分配数组，避免每帧 new

    private void UpdateTargetSelection()
    {
        float distMult = CombatSandbox.GetDist(1f);
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * distMult;
        int maxCount = Mathf.Max(1, (int)GetFinalWeaponStat(StatType.MultiShotCount));

        if (scanTimer <= 0f)
        {
            scanTimer = SCAN_INTERVAL;
            currentMultiTargets.Clear();

            // 1. 获取阵营环境
            bool IAmEnemy = false;
            if (mechRoot != null)
            {
                DamageReceiver dr = mechRoot.GetComponent<DamageReceiver>();
                IAmEnemy = (dr != null) ? dr.isEnemy : false;
            }

            // --- 👇【核心重构：指挥官手动集火判定】---
            ChimeraAIController parentAI = mechRoot.GetComponent<ChimeraAIController>();
            if (parentAI != null && parentAI.HasManualTarget())
            {
                Transform manualT = parentAI.GetManualTarget();

                // 判定手动目标是否有效 (存活、在射程内)
                if (IsTargetValid(manualT))
                {
                    float distToManual = Vector3.Distance(transform.position, manualT.position);
                    if (distToManual <= maxRange)
                    {
                        currentMultiTargets.Add(manualT);
                        lockedTarget = manualT;

                        // 如果是单发武器，锁定完直接下班
                        if (maxCount <= 1) return;
                    }
                }
            }
            // ------------------------------------------

            // 2. 【扫描阶段】：寻找周边的潜在线索 (NonAlloc)
            int mask = IAmEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, maxRange, results, mask);

            if (hitCount > 0)
            {
                // 确定当前的 AI 策略
                TargetingStrategy strategy = weaponData.SourceSO.TargetingOverride;
                if (strategy == TargetingStrategy.FollowCoreAI && ownerData != null)
                    strategy = ownerData.TargetingLogic;

                // 循环寻找，直到填满多重射击的配额 (maxCount)
                for (int k = currentMultiTargets.Count; k < maxCount; k++)
                {
                    DamageReceiver bestOne = null;
                    float bestScore = -float.MaxValue;

                    for (int i = 0; i < hitCount; i++)
                    {
                        DamageReceiver dr = results[i].GetComponentInParent<DamageReceiver>();

                        // 过滤：空引用、死亡、已在目标列表中（防止重复选中）
                        if (dr == null || dr.CurrentHP <= 0 || currentMultiTargets.Contains(dr.transform)) continue;

                        float dist = Vector3.Distance(transform.position, dr.transform.position);
                        float score = 0f;

                        // 执行 AI 评分策略
                        switch (strategy)
                        {
                            case TargetingStrategy.Furthest: score = dist; break;
                            case TargetingStrategy.MaxHPHighest: score = dr.MaxHP; break;
                            case TargetingStrategy.CurrentHPHighest: score = dr.CurrentHP; break;
                            default: score = -dist; break; // 默认就近
                        }

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestOne = dr;
                        }
                    }

                    if (bestOne != null) currentMultiTargets.Add(bestOne.transform);
                    else break; // 没怪了，跳出
                }
            }

            // 3. 更新主目标引用 (用于转向)
            if (currentMultiTargets.Count > 0) lockedTarget = currentMultiTargets[0];
            else lockedTarget = null;
        }
    }

    // 辅助方法：确保目标的有效性
    private bool IsTargetValid(Transform t)
    {
        if (t == null) return false;
        DamageReceiver dr = t.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0 && t.gameObject.activeInHierarchy;
    }
    private bool targetsExist(List<DamageReceiver> list) => list != null && list.Count > 0;

    private void FirePayload()
    {
        if (currentMultiTargets.Count == 0) return;
        if (myAnimator != null) myAnimator.SetTrigger("Fire");
        // 【关键修复】：获取当前机甲的真实阵营
        bool IAmEnemy = cachedIsEnemy;
        DamageReceiver dr = mechRoot.GetComponent<DamageReceiver>();
        if (dr != null) IAmEnemy = dr.isEnemy;

        // 👇【核心修复】：在跑开火积木前，先算出基础伤害种子
        // 这样霰弹枪积木才能拿到不为 0 的数值
        float seedMin = GetFinalWeaponStat(StatType.MinDamage);
        float seedMax = GetFinalWeaponStat(StatType.MaxDamage);
        float seedBaseDmg = Random.Range(seedMin, seedMax);

        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = muzzlePoint.position,
            PrimaryTarget = currentMultiTargets[0],
            BaseDamage = seedBaseDmg,
            SourceWeapon = weaponData,
            ChassisData = ownerData,
            IsEnemyFire = IAmEnemy, // 【修复】：不再是 false，而是跟随机甲阵营
            SourceEntity = mechRoot,
            TemporaryCritModifier = 1.0f,
            TemporaryDamageModifier = 1.0f
        };

        if (weaponData.OnFireActions != null) foreach (var a in weaponData.OnFireActions) { if (a != null) a.Execute(fireContext); if (fireContext.ExecutionAborted) return; }
        if (ownerData != null && ownerData.GlobalOnFireActions != null) foreach (var a in ownerData.GlobalOnFireActions) { if (a != null) a.Execute(fireContext); if (fireContext.ExecutionAborted) return; }

        // --- 阶段 B：分发管线 ---
        foreach (var target in currentMultiTargets)
        {
            if (target == null) continue;

            // 再次随机独立伤害（确保多目标不共用同一个数值）
            float baseDmg = Random.Range(seedMin, seedMax);
            float critChance = (GetFinalWeaponStat(StatType.CriticalChance) + weaponData.BonusCriticalChance) * fireContext.TemporaryCritModifier;
            bool isCrit = Random.value <= critChance;

            float damageToDeliver = baseDmg * fireContext.TemporaryDamageModifier;
            if (isCrit)
            {
                float critMult = GetFinalWeaponStat(StatType.CritMultiplier);
                if (critMult <= 1.0f) critMult = 2.0f; // 1.5保底
                damageToDeliver *= critMult;
            }

            ECAContext hitContext = new ECAContext
            {
                ImpactPoint = (weaponData.DeliveryType == WeaponDeliveryType.Melee) ? target.position : muzzlePoint.position,
                PrimaryTarget = target,
                BaseDamage = damageToDeliver,
                SourceWeapon = weaponData,
                ChassisData = ownerData,
                IsEnemyFire = false,
                SourceEntity = mechRoot,
                IsCriticalHit = isCrit
            };
            if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
            {
                GameObject projObj = SimplePool.Spawn(weaponData.ProjectilePrefab, muzzlePoint.position, actualHinge.rotation);
                Projectile pScript = projObj.GetComponent<Projectile>();
                if (pScript != null)
                {
                    pScript.Fire(
                        target,
                        damageToDeliver,
                        weaponData,
                        ownerData,
                        this.mechRoot,
                        IAmEnemy,
                        isCrit,
                        0,
                        false,
                        weaponData.ProjectilePrefab // 👈 传这个用于对象池归还
                    );
                }
            }
            else
            {
                if (weaponData.OnHitActions != null) foreach (var a in weaponData.OnHitActions) if (a != null) a.Execute(hitContext);
                if (ownerData != null && ownerData.GlobalOnHitActions != null) foreach (var a in ownerData.GlobalOnHitActions) if (a != null) a.Execute(hitContext);
            }
        }
    }

    private void InitiateAttack(Transform target)
    {
        float atkSpeed = GetFinalWeaponStat(StatType.AttackSpeed);
        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);
        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged) { FirePayload(); stateTimer = totalCooldown; return; }
        t_Windup = totalCooldown * weaponData.SourceSO.WindupTimeRatio;
        t_Strike = totalCooldown * weaponData.SourceSO.StrikeTimeRatio;
        t_Recovery = totalCooldown - t_Windup - t_Strike;
        rot_Base = Quaternion.AngleAxis(aimAngle, Vector3.forward);
        rot_Windup = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.WindupAngle);
        rot_Strike = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.StrikeAngle);
        currentState = WeaponState.Windup; stateTimer = t_Windup;
        if (myAnimator != null) myAnimator.SetTrigger("Windup");
    }

    private float GetFinalWeaponStat(StatType statID)
    {
        float val = weaponData.GetStat(statID);
        if (mechRoot != null)
        {
            var b = mechRoot.GetComponent<BuffManager>();
            if (b != null && b.BuffStatModifiers.ContainsKey(statID)) val += b.BuffStatModifiers[statID];
        }
        return val;
    }

    private bool IsTargetInRange(Transform target) { float dM = CombatSandbox.GetDist(1f); float maxR = GetFinalWeaponStat(StatType.MaxRange) * dM; Collider2D col = target.GetComponentInChildren<Collider2D>(); if (col == null) return Vector3.Distance(muzzlePoint.position, target.position) <= maxR; return Vector2.Distance(muzzlePoint.position, col.ClosestPoint(muzzlePoint.position)) <= maxR; }
    private Transform GetActualHinge() => (transform.name.StartsWith("Socket_") && transform.childCount > 0) ? transform.GetChild(0) : transform;
    private void UpdateMeleeAnimation() { float prg = 0; if (currentState == WeaponState.Windup) { prg = 1f - (stateTimer / t_Windup); actualHinge.rotation = Quaternion.Slerp(rot_Base, rot_Windup, prg); } else if (currentState == WeaponState.Strike) { prg = 1f - (stateTimer / t_Strike); actualHinge.rotation = Quaternion.Slerp(rot_Windup, rot_Strike, prg); } else if (currentState == WeaponState.Recovery) { prg = 1f - (stateTimer / t_Recovery); actualHinge.rotation = Quaternion.Slerp(rot_Strike, rot_Base, prg); } }
}