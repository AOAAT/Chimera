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
    // 👇【核心新增】：存储本次攻击选定的所有目标
    private List<Transform> currentMultiTargets = new List<Transform>();
    private float scanTimer = 0f;
    private const float SCAN_INTERVAL = 0.3f;

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
                if (IsTargetInRange(lockedTarget))
                {
                    InitiateAttack(lockedTarget);
                }
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
                case WeaponState.Windup:
                    currentState = WeaponState.Strike;
                    stateTimer = t_Strike;
                    break;
                case WeaponState.Strike:
                    // 👇 改为不传参，直接处理 currentMultiTargets 列表
                    FirePayload();
                    currentState = WeaponState.Recovery;
                    stateTimer = t_Recovery;
                    break;
                case WeaponState.Recovery:
                    currentState = WeaponState.Idle;
                    stateTimer = 0f;
                    break;
            }
        }

        if (weaponData.DeliveryType == WeaponDeliveryType.Melee && currentState != WeaponState.Idle)
        {
            UpdateMeleeAnimation();
        }
    }

    private bool IsTargetInRange(Transform target)
    {
        float distMult = CombatSandbox.GetDist(1f);
        float maxR = GetFinalWeaponStat(StatType.MaxRange) * distMult;
        Collider2D targetCol = target.GetComponentInChildren<Collider2D>();
        if (targetCol == null) return Vector3.Distance(muzzlePoint.position, target.position) <= maxR;
        float surfaceDist = Vector2.Distance(muzzlePoint.position, targetCol.ClosestPoint(muzzlePoint.position));
        return surfaceDist <= maxR;
    }

    private void UpdateTargetSelection()
    {
        float distMult = CombatSandbox.GetDist(1f);
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * distMult;
        // 👇 获取本级图纸设定的最大攻击数量
        int maxCount = Mathf.Max(1, (int)GetFinalWeaponStat(StatType.MultiShotCount));

        if (scanTimer <= 0f)
        {
            scanTimer = SCAN_INTERVAL;
            int mask = LayerMask.GetMask("Enemy_Hitbox");
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, maxRange, mask);

            if (hits.Length > 0)
            {
                TargetingStrategy strategy = weaponData.SourceSO.TargetingOverride;
                if (strategy == TargetingStrategy.FollowCoreAI && ownerData != null)
                {
                    strategy = ownerData.TargetingLogic;
                }

                // 2. 构建候选列表并执行排序
                var targets = hits.Select(h => h.GetComponentInParent<DamageReceiver>())
                                  .Where(dr => dr != null && dr.CurrentHP > 0)
                                  .OrderBy(dr => (strategy == TargetingStrategy.Furthest) ?
                                      -Vector3.Distance(transform.position, dr.transform.position) :
                                       Vector3.Distance(transform.position, dr.transform.position))
                                  .Take(maxCount) // 👈 抓取指定数量的目标
                                  .ToList();

                if (targets.Count > 0)
                {
                    currentMultiTargets = targets.Select(t => t.transform).ToList();
                    lockedTarget = currentMultiTargets[0]; // 主目标用于朝向
                }
                else { lockedTarget = null; currentMultiTargets.Clear(); }
            }
            else { lockedTarget = null; currentMultiTargets.Clear(); }
        }
    }

    private void InitiateAttack(Transform target)
    {
        float atkSpeed = GetFinalWeaponStat(StatType.AttackSpeed);
        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            FirePayload(); // 👈 远程同样进入多重分发
            stateTimer = totalCooldown;
            return;
        }

        t_Windup = totalCooldown * weaponData.SourceSO.WindupTimeRatio;
        t_Strike = totalCooldown * weaponData.SourceSO.StrikeTimeRatio;
        t_Recovery = totalCooldown - t_Windup - t_Strike;
        rot_Base = Quaternion.AngleAxis(aimAngle, Vector3.forward);
        rot_Windup = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.WindupAngle);
        rot_Strike = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.StrikeAngle);
        currentState = WeaponState.Windup;
        stateTimer = t_Windup;
        if (myAnimator != null) myAnimator.SetTrigger("Windup");
    }

    private void UpdateMeleeAnimation()
    {
        float progress = 0;
        if (currentState == WeaponState.Windup) { progress = 1f - (stateTimer / t_Windup); actualHinge.rotation = Quaternion.Slerp(rot_Base, rot_Windup, progress); }
        else if (currentState == WeaponState.Strike) { progress = 1f - (stateTimer / t_Strike); actualHinge.rotation = Quaternion.Slerp(rot_Windup, rot_Strike, progress); }
        else if (currentState == WeaponState.Recovery) { progress = 1f - (stateTimer / t_Recovery); actualHinge.rotation = Quaternion.Slerp(rot_Strike, rot_Base, progress); }
    }

    private void FirePayload()
    {
        if (currentMultiTargets.Count == 0) return;
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        // --- 阶段 A：全局开火管线 (跑一次，处理扣蓝等) ---
        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = muzzlePoint.position,
            PrimaryTarget = currentMultiTargets[0],
            BaseDamage = 0, // 仅作为信号，不产生直接伤害
            SourceWeapon = weaponData,
            ChassisData = ownerData,
            IsEnemyFire = false,
            SourceEntity = mechRoot,
            TemporaryCritModifier = 1.0f,
            TemporaryDamageModifier = 1.0f
        };

        if (weaponData.OnFireActions != null) foreach (var a in weaponData.OnFireActions) { a.Execute(fireContext); if (fireContext.ExecutionAborted) return; }
        if (ownerData != null && ownerData.GlobalOnFireActions != null) foreach (var a in ownerData.GlobalOnFireActions) { a.Execute(fireContext); if (fireContext.ExecutionAborted) return; }

        // --- 阶段 B：多重分发管线 (遍历每一个目标) ---
        foreach (var target in currentMultiTargets)
        {
            if (target == null) continue;

            // 为每个子目标独立计算随机伤害和暴击
            float baseDmg = Random.Range(GetFinalWeaponStat(StatType.MinDamage), GetFinalWeaponStat(StatType.MaxDamage));
            float critChance = (GetFinalWeaponStat(StatType.CriticalChance) + weaponData.BonusCriticalChance) * fireContext.TemporaryCritModifier;
            bool isCrit = Random.value <= critChance;

            float damageToDeliver = baseDmg * fireContext.TemporaryDamageModifier;
            if (isCrit)
            {
                float critMult = GetFinalWeaponStat(StatType.CritMultiplier);
                if (critMult <= 0) critMult = 1.5f;
                damageToDeliver *= critMult;
            }

            ECAContext hitContext = new ECAContext
            {
                ImpactPoint = (weaponData.DeliveryType == WeaponDeliveryType.Melee) ? target.position : muzzlePoint.position,
                PrimaryTarget = target,
                BaseDamage = damageToDeliver, // 👈 核心：将算好的伤害传给命中积木
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
                    pScript.Fire(target, damageToDeliver, weaponData, ownerData, mechRoot, false, isCrit, 0, false);
                }
            }
            else
            {
                // 👇【星星法杖核心】：Melee 模式下，针对每个目标依次触发“天火打击”
                if (weaponData.OnHitActions != null) foreach (var a in weaponData.OnHitActions) if (a != null) a.Execute(hitContext);
                if (ownerData != null && ownerData.GlobalOnHitActions != null) foreach (var a in ownerData.GlobalOnHitActions) if (a != null) a.Execute(hitContext);
            }
        }
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

    private Transform GetActualHinge() { return (transform.name.StartsWith("Socket_") && transform.childCount > 0) ? transform.GetChild(0) : transform; }
}