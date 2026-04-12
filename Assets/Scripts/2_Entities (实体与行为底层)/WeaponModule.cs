// --- START OF FILE WeaponModule.cs ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private enum WeaponState { Idle, Windup, Strike, Recovery }
    private WeaponState currentState = WeaponState.Idle;

    private RuntimeWeapon weaponData;
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

    [Header("调试信息")]
    public List<Transform> CurrentTargets = new List<Transform>();

    public void Initialize(RuntimeWeapon data, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        logicCenterOffset = centerOffset;
        mechRoot = root;

        actualHinge = GetActualHinge();
        if (actualHinge.childCount > 0) myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset;
        muzzlePoint = muzzleObj.transform;

        currentState = WeaponState.Idle;
        stateTimer = 0f;
    }

    public Vector3 GetLogicCenter()
    {
        if (mechRoot != null) return mechRoot.TransformPoint(logicCenterOffset);
        return transform.position;
    }

    private Transform GetActualHinge()
    {
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0) return transform.GetChild(0);
        return transform;
    }

    // 👇【核心枢纽】：所有的武器数值，都必须经过这里的洗礼，叠加 Buff 增益！
    private float GetFinalWeaponStat(StatType statID)
    {
        float baseValue = weaponData.GetStat(statID);

        if (mechRoot != null)
        {
            BuffManager buffMgr = mechRoot.GetComponent<BuffManager>();
            if (buffMgr != null && buffMgr.BuffStatModifiers.ContainsKey(statID))
            {
                baseValue += buffMgr.BuffStatModifiers[statID];
            }
        }
        return baseValue;
    }

    private float GetMaxDistanceFromBounds(Vector2 center, Bounds bounds)
    {
        Vector2 min = bounds.min; Vector2 max = bounds.max;
        float d1 = Vector2.SqrMagnitude(center - new Vector2(min.x, min.y));
        float d2 = Vector2.SqrMagnitude(center - new Vector2(max.x, min.y));
        float d3 = Vector2.SqrMagnitude(center - new Vector2(min.x, max.y));
        float d4 = Vector2.SqrMagnitude(center - new Vector2(max.x, max.y));
        return Mathf.Sqrt(Mathf.Max(d1, Mathf.Max(d2, Mathf.Max(d3, d4))));
    }

    private void FindTarget()
    {
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;

        // 👇 替换为最终属性
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * distMult;
        float minRange = GetFinalWeaponStat(StatType.MinRange) * distMult;
        int maxLockCount = Mathf.Max((int)GetFinalWeaponStat(StatType.MultiShotCount), 1);

        Vector3 center = GetLogicCenter();
        DamageReceiver myReceiver = mechRoot.GetComponent<DamageReceiver>();
        bool amIEnemy = (myReceiver != null && myReceiver.isEnemy);
        int targetLayerMask = amIEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, maxRange, targetLayerMask);

        CurrentTargets = hits
            .Select(hit => new { Collider = hit, Receiver = hit.GetComponentInParent<DamageReceiver>() })
            .Where(x => x.Receiver != null && x.Receiver.CurrentHP > 0)
            .Where(x => GetMaxDistanceFromBounds(center, x.Collider.bounds) >= minRange)
            .GroupBy(x => x.Receiver).Select(group => group.First())
            .OrderBy(x => Vector2.Distance(center, x.Collider.ClosestPoint(center)))
            .Take(maxLockCount).Select(x => x.Receiver.transform).ToList();
    }

    private void Update()
    {
        if (weaponData == null || actualHinge == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        FindTarget();
        Transform primaryTarget = (CurrentTargets.Count > 0) ? CurrentTargets[0] : null;

        switch (currentState)
        {
            case WeaponState.Idle:
                if (primaryTarget != null)
                {
                    Vector3 targetCenter = GetTargetCenter(primaryTarget);
                    Vector3 aimDir = targetCenter - actualHinge.position;
                    aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    actualHinge.rotation = Quaternion.AngleAxis(aimAngle, Vector3.forward);

                    if (stateTimer <= 0f) InitiateAttack();
                }
                if (stateTimer > 0f) stateTimer -= Time.deltaTime;
                break;
            case WeaponState.Windup:
                stateTimer -= Time.deltaTime;
                float windupProgress = 1f - (stateTimer / t_Windup);
                actualHinge.rotation = Quaternion.Slerp(rot_Base, rot_Windup, windupProgress);
                if (stateTimer <= 0f) { currentState = WeaponState.Strike; stateTimer = t_Strike; }
                break;
            case WeaponState.Strike:
                stateTimer -= Time.deltaTime;
                float strikeProgress = 1f - (stateTimer / t_Strike);
                actualHinge.rotation = Quaternion.Slerp(rot_Windup, rot_Strike, strikeProgress);
                if (stateTimer <= 0f) { FirePayload(); currentState = WeaponState.Recovery; stateTimer = t_Recovery; }
                break;
            case WeaponState.Recovery:
                stateTimer -= Time.deltaTime;
                float recoveryProgress = 1f - (stateTimer / t_Recovery);
                actualHinge.rotation = Quaternion.Slerp(rot_Strike, rot_Base, recoveryProgress);
                if (stateTimer <= 0f) { currentState = WeaponState.Idle; stateTimer = 0f; }
                break;
        }
    }

    private void InitiateAttack()
    {
        // 👇 替换为最终属性（享受超频 Buff 攻速加成！）
        float atkSpeed = GetFinalWeaponStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 100f;

        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            FirePayload();
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

    private void FirePayload()
    {
        if (CurrentTargets.Count == 0) return;
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        // 👇 替换为最终属性（享受狂暴 Buff 伤害/暴击加成！）
        float safeMinDmg = Mathf.Max(0f, GetFinalWeaponStat(StatType.MinDamage));
        float safeMaxDmg = Mathf.Max(safeMinDmg, GetFinalWeaponStat(StatType.MaxDamage));

        float finalDmg = Random.Range(safeMinDmg, safeMaxDmg);
        float totalCritChance = GetFinalWeaponStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;
        if (isCrit) finalDmg *= 1.5f;

        ECAContext fireContext = new ECAContext { ImpactPoint = muzzlePoint.position, PrimaryTarget = CurrentTargets[0], BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false, SourceEntity = mechRoot };
        if (weaponData.OnFireActions != null)
        {
            foreach (var action in weaponData.OnFireActions)
            {
                if (action != null) { action.Execute(fireContext); if (fireContext.ExecutionAborted) return; }
            }
        }

        foreach (var target in CurrentTargets)
        {
            Vector3 visualTargetCenter = GetTargetCenter(target);
            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext { ImpactPoint = visualTargetCenter, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false };
                if (weaponData.OnHitActions != null) foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(hitContext);
            }
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                Vector3 bulletDir = visualTargetCenter - muzzlePoint.position;
                float bulletAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, muzzlePoint.position, Quaternion.AngleAxis(bulletAngle, Vector3.forward));
                projObj.GetComponent<Projectile>().Fire(target, finalDmg, weaponData, false, isCrit);
            }
        }
    }

    private Vector3 GetTargetCenter(Transform target)
    {
        if (target == null) return Vector3.zero;
        Collider2D targetCol = target.GetComponentInChildren<Collider2D>();
        return targetCol != null ? targetCol.bounds.center : target.position;
    }
}