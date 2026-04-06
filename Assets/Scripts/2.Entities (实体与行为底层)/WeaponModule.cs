// --- START OF FILE WeaponModule.cs ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private RuntimeWeapon weaponData;
    private float fireCooldown = 0f;

    private Vector2 logicCenterOffset;
    private Transform mechRoot;

    private Transform muzzlePoint;
    private Animator myAnimator;

    [Header("调试信息")]
    public List<Transform> CurrentTargets = new List<Transform>();

    public void Initialize(RuntimeWeapon data, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        logicCenterOffset = centerOffset;
        mechRoot = root;

        Transform actualHinge = GetActualHinge();

        if (actualHinge.childCount > 0)
            myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset;
        muzzlePoint = muzzleObj.transform;
    }

    public Vector3 GetLogicCenter()
    {
        if (mechRoot != null) return mechRoot.TransformPoint(logicCenterOffset);
        return transform.position;
    }

    private Transform GetActualHinge()
    {
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0)
        {
            return transform.GetChild(0);
        }
        return transform;
    }

    private void Update()
    {
        if (weaponData == null) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        fireCooldown -= Time.deltaTime;
        FindTarget();

        if (CurrentTargets != null && CurrentTargets.Count > 0)
        {
            Transform primaryTarget = CurrentTargets[0];
            if (primaryTarget != null)
            {
                // 👇【视觉优化】：枪管转动时，也死死盯住敌人的物理中心，而不是边缘点！
                Transform actualHinge = GetActualHinge();
                Vector3 targetCenter = primaryTarget.position;
                Collider2D targetCol = primaryTarget.GetComponentInChildren<Collider2D>();
                if (targetCol != null) targetCenter = targetCol.bounds.center;

                Vector3 aimDir = targetCenter - actualHinge.position;
                float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                actualHinge.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if (fireCooldown <= 0f) Fire();
        }
    }

    private float GetMaxDistanceFromBounds(Vector2 center, Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        float d1 = Vector2.SqrMagnitude(center - new Vector2(min.x, min.y));
        float d2 = Vector2.SqrMagnitude(center - new Vector2(max.x, min.y));
        float d3 = Vector2.SqrMagnitude(center - new Vector2(min.x, max.y));
        float d4 = Vector2.SqrMagnitude(center - new Vector2(max.x, max.y));

        return Mathf.Sqrt(Mathf.Max(d1, Mathf.Max(d2, Mathf.Max(d3, d4))));
    }

    private void FindTarget()
    {
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;
        float maxRange = weaponData.GetStat(StatType.MaxRange) * distMult;
        float minRange = weaponData.GetStat(StatType.MinRange) * distMult;
        int maxLockCount = Mathf.Max((int)weaponData.GetStat(StatType.MultiShotCount), 1);
        Vector3 center = GetLogicCenter();

        DamageReceiver myReceiver = mechRoot.GetComponent<DamageReceiver>();
        bool amIEnemy = (myReceiver != null && myReceiver.isEnemy);

        int targetLayerMask = amIEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, maxRange, targetLayerMask);

        CurrentTargets = hits
            .Select(hit => {
                DamageReceiver r = hit.GetComponentInParent<DamageReceiver>();
                return new { Collider = hit, Receiver = r };
            })
            .Where(x => x.Receiver != null && x.Receiver.CurrentHP > 0)
            .Where(x => {
                float distToFurthest = GetMaxDistanceFromBounds(center, x.Collider.bounds);
                return distToFurthest >= minRange;
            })
            .GroupBy(x => x.Receiver)
            .Select(group => group.First())
            .OrderBy(x => {
                // 👇【逻辑不变】：雷达排序依然查边缘，保证优先打离自己最近的怪
                Vector2 closestPoint = x.Collider.ClosestPoint(center);
                return Vector2.Distance(center, closestPoint);
            })
            .Take(maxLockCount)
            .Select(x => x.Receiver.transform)
            .ToList();
    }

    private void Fire()
    {
        float atkSpeed = weaponData.GetStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 100f;

        fireCooldown = GameFormulas.CalcCooldown(atkSpeed);

        if (CurrentTargets.Count == 0) return;

        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        float finalDmg = Random.Range(weaponData.GetStat(StatType.MinDamage), weaponData.GetStat(StatType.MaxDamage));
        float totalCritChance = weaponData.GetStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;
        if (isCrit) finalDmg *= 1.5f;

        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = muzzlePoint.position,
            PrimaryTarget = CurrentTargets[0],
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            IsCriticalHit = isCrit,
            IsEnemyFire = false
        };

        if (weaponData.OnFireActions != null)
            foreach (var action in weaponData.OnFireActions)
                if (action != null) action.Execute(fireContext);

        foreach (var target in CurrentTargets)
        {
            // 👇【视觉核心修复】：找到敌人的绝对包围盒中心点，把子弹/激光强行按在中心！
            Vector3 visualTargetCenter = target.position;
            Collider2D targetCol = target.GetComponentInChildren<Collider2D>();
            if (targetCol != null) visualTargetCenter = targetCol.bounds.center;

            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext { ImpactPoint = visualTargetCenter, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false };
                if (weaponData.OnHitActions != null) foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(hitContext);

                Debug.DrawLine(muzzlePoint.position, visualTargetCenter, Color.yellow, 0.1f);
            }
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                // 👇【投递修正】：子弹也向敌人的中心点飞去！
                Vector3 bulletDir = visualTargetCenter - muzzlePoint.position;
                float bulletAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
                Quaternion bulletRot = Quaternion.AngleAxis(bulletAngle, Vector3.forward);

                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, muzzlePoint.position, bulletRot);
                Projectile projectile = projObj.GetComponent<Projectile>();

                // 【投递目标修正】：虽然它是追着 target(Transform) 飞的，但如果在贴脸距离，
                // Projectile 会瞬间在 target.position 引爆。由于我们改了枪管朝向，激光看起来会完美穿透到敌人中心！
                projectile.Fire(target, finalDmg, weaponData, false, isCrit);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (weaponData == null) return;

        float distanceMultiplier = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;
        float maxRange = weaponData.GetStat(StatType.MaxRange) * distanceMultiplier;
        float minRange = weaponData.GetStat(StatType.MinRange) * distanceMultiplier;

        Vector3 center = GetLogicCenter();
        Gizmos.color = new Color(0, 0, 1f, 0.3f);
        Gizmos.DrawWireSphere(center, maxRange);

        if (minRange > 0f)
        {
            Gizmos.color = new Color(1f, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(center, minRange);
        }
    }
}