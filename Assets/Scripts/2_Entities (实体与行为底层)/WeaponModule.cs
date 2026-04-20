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

    // 👇【核心修复】：目标粘性与扫描优化
    private Transform lockedTarget;
    private float scanTimer = 0f;
    private const float SCAN_INTERVAL = 0.4f; // 每 0.4 秒才准扫描一次新目标
    private const float RANGE_BUFFER = 1.15f; // 锁定后，敌人跑出 115% 射程才会断开锁定

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
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset * CombatSandbox.GetDist(1f);
        muzzlePoint = muzzleObj.transform;

        currentState = WeaponState.Idle;
        stateTimer = 0f;
    }

    private void Update()
    {
        if (weaponData == null || actualHinge == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        // 1. 智能索敌（带粘性，防止鬼畜）
        UpdateTargetSelection();

        // 2. 行为表现
        if (lockedTarget != null)
        {
            // 指向锁定目标
            Vector3 aimDir = lockedTarget.position - actualHinge.position;
            aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

            // 平滑转向（可选）：让转向更有机械质感
            actualHinge.rotation = Quaternion.RotateTowards(actualHinge.rotation, Quaternion.AngleAxis(aimAngle, Vector3.forward), 720f * Time.deltaTime);

            if (currentState == WeaponState.Idle && stateTimer <= 0f)
            {
                InitiateAttack(lockedTarget);
            }
        }

        if (stateTimer > 0f) stateTimer -= Time.deltaTime;
        if (scanTimer > 0f) scanTimer -= Time.deltaTime;
    }

    private void UpdateTargetSelection()
    {
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * (CombatSandbox.Instance?.DistanceMultiplier ?? 1f);

        // --- 逻辑 A：已有目标，检查是否需要断开 ---
        if (lockedTarget != null)
        {
            DamageReceiver dr = lockedTarget.GetComponentInParent<DamageReceiver>();
            float dist = Vector3.Distance(transform.position, lockedTarget.position);

            // 如果目标没死，且没跑得太远（给个 15% 的缓冲区），就死磕它，不许换人！
            if (dr != null && dr.CurrentHP > 0 && dist <= maxRange * RANGE_BUFFER)
            {
                return; // 继续锁定，直接跳过后面的扫描
            }
            else
            {
                lockedTarget = null; // 目标丢失
            }
        }

        // --- 逻辑 B：没有目标，或者旧目标跑了，定时扫描全场 ---
        if (scanTimer <= 0f)
        {
            scanTimer = SCAN_INTERVAL;
            int mask = LayerMask.GetMask("Enemy_Hitbox");
            Collider2D hit = Physics2D.OverlapCircle(transform.position, maxRange, mask);
            if (hit != null) lockedTarget = hit.transform;
        }
    }

    private void InitiateAttack(Transform target)
    {
        float atkSpeed = GetFinalWeaponStat(StatType.AttackSpeed);
        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged) { FirePayload(target); stateTimer = totalCooldown; return; }

        t_Windup = totalCooldown * weaponData.SourceSO.WindupTimeRatio;
        t_Strike = totalCooldown * weaponData.SourceSO.StrikeTimeRatio;
        t_Recovery = totalCooldown - t_Windup - t_Strike;
        currentState = WeaponState.Windup; stateTimer = t_Windup;
    }

    private void FirePayload(Transform target)
    {
        if (target == null) return;
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        float finalDmg = Random.Range(GetFinalWeaponStat(StatType.MinDamage), GetFinalWeaponStat(StatType.MaxDamage));
        bool isCrit = Random.value <= (GetFinalWeaponStat(StatType.CriticalChance) + weaponData.BonusCriticalChance);
        if (isCrit) finalDmg *= 1.5f;

        ECAContext context = new ECAContext
        {
            ImpactPoint = muzzlePoint.position,
            PrimaryTarget = target,
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            ChassisData = ownerData,
            IsCriticalHit = isCrit,
            IsEnemyFire = false,
            SourceEntity = mechRoot
        };

        // 执行开火管线
        if (weaponData.OnFireActions != null) foreach (var a in weaponData.OnFireActions) { a.Execute(context); if (context.ExecutionAborted) return; }
        if (ownerData != null && ownerData.GlobalOnFireActions != null) foreach (var a in ownerData.GlobalOnFireActions) { a.Execute(context); if (context.ExecutionAborted) return; }

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            GameObject projObj = SimplePool.Spawn(weaponData.ProjectilePrefab, muzzlePoint.position, actualHinge.rotation);
            Projectile pScript = projObj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 参数顺序：目标, 伤害, 武器, 玩家黑盒, 自身, 是否怪弹, 是否暴击, 代际, 是否奶弹
                pScript.Fire(target, context.BaseDamage, weaponData, ownerData, mechRoot, false, isCrit, 0, false);
            }
        }
        else
        {
            context.ImpactPoint = target.position;
            if (weaponData.OnHitActions != null) foreach (var a in weaponData.OnHitActions) a.Execute(context);
            if (ownerData != null && ownerData.GlobalOnHitActions != null) foreach (var a in ownerData.GlobalOnHitActions) a.Execute(context);
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