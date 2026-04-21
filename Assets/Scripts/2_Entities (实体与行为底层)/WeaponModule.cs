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
                // 只有在射程内才启动攻击逻辑
                if (IsTargetInRange(lockedTarget))
                {
                    InitiateAttack(lockedTarget);
                }
            }
        }

        // 状态机计时
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
                    // 如果是近战，动画可以播放下劈
                    break;
                case WeaponState.Strike:
                    FirePayload(lockedTarget);
                    currentState = WeaponState.Recovery;
                    stateTimer = t_Recovery;
                    break;
                case WeaponState.Recovery:
                    currentState = WeaponState.Idle;
                    stateTimer = 0f;
                    break;
            }
        }

        // 动作表现插值 (仅近战有效)
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

        // 👇【核心修复】：计算枪口到敌人碰撞体“表面”的真实距离
        float surfaceDist = Vector2.Distance(muzzlePoint.position, targetCol.ClosestPoint(muzzlePoint.position));
        return surfaceDist <= maxR;
    }

    private void UpdateTargetSelection()
    {
        float distMult = CombatSandbox.GetDist(1f);
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * distMult;

        // 如果是近战，扫描半径给 1.5 米的宽限，确保能先锁定并走向敌人
        float scanRange = weaponData.DeliveryType == WeaponDeliveryType.Melee ? maxRange + 1.5f : maxRange;

        if (lockedTarget != null)
        {
            DamageReceiver dr = lockedTarget.GetComponentInParent<DamageReceiver>();
            if (dr == null || dr.CurrentHP <= 0 || !IsTargetInRange(lockedTarget))
            {
                // 如果跑得太远（超过扫描范围），才丢失目标
                if (lockedTarget != null && Vector3.Distance(transform.position, lockedTarget.position) > scanRange * 1.2f)
                    lockedTarget = null;
            }
            else return;
        }

        if (scanTimer <= 0f)
        {
            scanTimer = SCAN_INTERVAL;
            int mask = LayerMask.GetMask("Enemy_Hitbox");
            Collider2D hit = Physics2D.OverlapCircle(transform.position, scanRange, mask);
            if (hit != null) lockedTarget = hit.transform;
        }
    }

    private void InitiateAttack(Transform target)
    {
        float atkSpeed = GetFinalWeaponStat(StatType.AttackSpeed);
        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            FirePayload(target);
            stateTimer = totalCooldown;
            return;
        }

        // 近战三段式切分
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
        // 简单的角度插值，模拟挥砍动作
        float progress = 0;
        if (currentState == WeaponState.Windup)
        {
            progress = 1f - (stateTimer / t_Windup);
            actualHinge.rotation = Quaternion.Slerp(rot_Base, rot_Windup, progress);
        }
        else if (currentState == WeaponState.Strike)
        {
            progress = 1f - (stateTimer / t_Strike);
            actualHinge.rotation = Quaternion.Slerp(rot_Windup, rot_Strike, progress);
        }
        else if (currentState == WeaponState.Recovery)
        {
            progress = 1f - (stateTimer / t_Recovery);
            actualHinge.rotation = Quaternion.Slerp(rot_Strike, rot_Base, progress);
        }
    }

    private void FirePayload(Transform target)
    {
        if (target == null) return;
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        // 1. 基础随机 (先不算暴击)
        float finalDmg = Random.Range(GetFinalWeaponStat(StatType.MinDamage), GetFinalWeaponStat(StatType.MaxDamage));

        ECAContext context = new ECAContext
        {
            ImpactPoint = (weaponData.DeliveryType == WeaponDeliveryType.Melee) ? target.position : muzzlePoint.position,
            PrimaryTarget = target,
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            ChassisData = ownerData,
            IsEnemyFire = false,
            SourceEntity = mechRoot,
            TemporaryCritModifier = 1.0f, // 👈 初始化倍率
            TemporaryDamageModifier = 1.0f
        };

        // 2. 执行所有 OnFire 积木 (此时狮子头积木会修改 context.TemporaryCritModifier)
        if (weaponData.OnFireActions != null) foreach (var a in weaponData.OnFireActions) { a.Execute(context); if (context.ExecutionAborted) return; }
        if (ownerData != null && ownerData.GlobalOnFireActions != null) foreach (var a in ownerData.GlobalOnFireActions) { a.Execute(context); if (context.ExecutionAborted) return; }

        // 3. 【核心判定点】：根据积木运算后的“新倍率”来决定是否暴击
        // 公式：最终暴击率 = (武器基础暴击率 + Buff暴击率) * 狮子头倍率
        float totalCritChance = (GetFinalWeaponStat(StatType.CriticalChance) + weaponData.BonusCriticalChance) * context.TemporaryCritModifier;
        bool isCrit = Random.value <= totalCritChance;

        context.IsCriticalHit = isCrit; // 标记结果，供后续命中积木查看

        // 最终伤害 = context.BaseDamage (可能已被电磁炮加过) * 狮子头伤害倍率
        float damageToDeliver = context.BaseDamage * context.TemporaryDamageModifier;
        if (isCrit) damageToDeliver *= 1.5f;

        // 4. 正式发射
        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            GameObject projObj = SimplePool.Spawn(weaponData.ProjectilePrefab, muzzlePoint.position, actualHinge.rotation);
            Projectile pScript = projObj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 注意：这里传的是已经应用了倍率的最终伤害
                pScript.Fire(target, damageToDeliver, weaponData, ownerData, mechRoot, false, isCrit, 0, false);
            }
        }
        else
        {
            // 近战直接传带倍率的 Context 即可
            context.BaseDamage = damageToDeliver;
            if (weaponData.OnHitActions != null) foreach (var a in weaponData.OnHitActions) if (a != null) a.Execute(context);
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