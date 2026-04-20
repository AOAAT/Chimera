using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private enum WeaponState { Idle, Windup, Strike, Recovery }
    private WeaponState currentState = WeaponState.Idle;

    private RuntimeWeapon weaponData;
    private RuntimeChimeraData ownerData; // 核心：记住是谁装的我

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

    public void Initialize(RuntimeWeapon data, RuntimeChimeraData owner, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        ownerData = owner; // 存下主人黑盒
        logicCenterOffset = centerOffset;
        mechRoot = root;

        actualHinge = GetActualHinge();
        if (actualHinge.childCount > 0) myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);

        // 【度量衡修复】：视觉枪口位置偏移
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset * distMult;
        muzzlePoint = muzzleObj.transform;

        currentState = WeaponState.Idle;
        stateTimer = 0f;
    }

    private Transform GetActualHinge()
    {
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0) return transform.GetChild(0);
        return transform;
    }

    private float GetFinalWeaponStat(StatType statID)
    {
        float baseValue = weaponData.GetStat(statID);
        if (mechRoot != null)
        {
            BuffManager buffMgr = mechRoot.GetComponent<BuffManager>();
            if (buffMgr != null && buffMgr.BuffStatModifiers.ContainsKey(statID)) baseValue += buffMgr.BuffStatModifiers[statID];
        }
        return baseValue;
    }

    private void Update()
    {
        if (weaponData == null || actualHinge == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        // 简化的索敌逻辑，保持与 AI 目标一致
        Transform primaryTarget = FindPrimaryTarget();

        switch (currentState)
        {
            case WeaponState.Idle:
                if (primaryTarget != null)
                {
                    Vector3 aimDir = primaryTarget.position - actualHinge.position;
                    aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    actualHinge.rotation = Quaternion.AngleAxis(aimAngle, Vector3.forward);
                    if (stateTimer <= 0f) InitiateAttack(primaryTarget);
                }
                if (stateTimer > 0f) stateTimer -= Time.deltaTime;
                break;
            case WeaponState.Windup:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) { currentState = WeaponState.Strike; stateTimer = t_Strike; }
                break;
            case WeaponState.Strike:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) { FirePayload(primaryTarget); currentState = WeaponState.Recovery; stateTimer = t_Recovery; }
                break;
            case WeaponState.Recovery:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) { currentState = WeaponState.Idle; stateTimer = 0f; }
                break;
        }
    }

    private Transform FindPrimaryTarget()
    {
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;
        float maxRange = GetFinalWeaponStat(StatType.MaxRange) * distMult;
        int mask = LayerMask.GetMask("Enemy_Hitbox");
        Collider2D hit = Physics2D.OverlapCircle(transform.position, maxRange, mask);
        return hit != null ? hit.transform : null;
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

        ECAContext context = new ECAContext { ImpactPoint = muzzlePoint.position, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, ChassisData = ownerData, IsCriticalHit = isCrit, IsEnemyFire = false, SourceEntity = mechRoot };

        if (weaponData.OnFireActions != null)
        {
            foreach (var a in weaponData.OnFireActions) { if (a != null) a.Execute(context); if (context.ExecutionAborted) return; }
        }

        if (ownerData != null && ownerData.GlobalOnFireActions != null)
        {
            foreach (var a in ownerData.GlobalOnFireActions) { if (a != null) a.Execute(context); if (context.ExecutionAborted) return; }
        }

        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            GameObject projObj = SimplePool.Spawn(weaponData.ProjectilePrefab, muzzlePoint.position, actualHinge.rotation);
            Projectile pScript = projObj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 👇【关键修复】：传入 mechRoot 作为 shooter
                pScript.Fire(target, finalDmg, weaponData, ownerData, mechRoot, false, isCrit, 0, false);
            }
        }
        else
        {
            if (weaponData.OnHitActions != null) foreach (var a in weaponData.OnHitActions) if (a != null) a.Execute(context);
            if (ownerData != null && ownerData.GlobalOnHitActions != null) foreach (var a in ownerData.GlobalOnHitActions) if (a != null) a.Execute(context);
        }
    }

}