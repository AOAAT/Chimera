using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageReceiver))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    public EnemyDataSO MyData;

    private DamageReceiver myReceiver;
    private Rigidbody2D rb;
    private Transform currentTarget;
    private RuntimeWeapon dummyWeaponForECA;

    private float attackCooldownTimer = 0f;
    private float moveSkillTimer = 0f;
    private bool isFleeing = false;
    private bool isUsingMoveSkill = false;

    private float lastFrameHP;
    private bool isDead = false;

    // 调试专用计时器，防止 Log 刷屏
    private float debugLogTimer = 1f;

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }

        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();
        lastFrameHP = myReceiver.CurrentHP;

        dummyWeaponForECA = new RuntimeWeapon
        {
            WeaponName = MyData.EnemyName,
            DeliveryType = MyData.DeliveryType,
            ProjectilePrefab = MyData.ProjectilePrefab,
            SourceSO = null
        };

        dummyWeaponForECA.WeaponStats[StatType.MaxDamage] = MyData.GetStat(StatType.MaxDamage);
        dummyWeaponForECA.WeaponStats[StatType.MinDamage] = MyData.GetStat(StatType.MinDamage);
        dummyWeaponForECA.WeaponStats[StatType.ExplosionRadius] = MyData.GetStat(StatType.ExplosionRadius);

        foreach (var stat in MyData.BaseStats)
        {
            dummyWeaponForECA.WeaponStats[stat.StatID] = stat.Value;
        }

        if (MyData.OnAttackHitActions != null)
            dummyWeaponForECA.OnHitActions.AddRange(MyData.OnAttackHitActions);

        ExecuteECAActions(MyData.OnSpawnActions, this.transform);
        Debug.Log($"<color=#00FF00>【大脑启动】</color> {MyData.EnemyName} 已上线，最大速度配置为: {MyData.GetStat(StatType.MoveSpeed)}，质量: {rb.mass}");
    }

    private void Update()
    {
        if (isDead) return;

        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, this.transform);
            lastFrameHP = myReceiver.CurrentHP;
        }

        if (myReceiver.CurrentHP <= 0)
        {
            isDead = true;
            rb.velocity = Vector2.zero;
            ExecuteECAActions(MyData.OnDeathActions, this.transform);
            return;
        }

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (!isUsingMoveSkill) rb.velocity = Vector2.zero;
            return;
        }

        attackCooldownTimer -= Time.deltaTime;
        moveSkillTimer -= Time.deltaTime;

        FindTarget();
        HandleMovementAndCombat();
        PrintDebugStatus();
    }

    private void PrintDebugStatus()
    {
        debugLogTimer -= Time.deltaTime;
        if (debugLogTimer <= 0)
        {
            debugLogTimer = 1f; // 每 1 秒打印一次状态
            string targetName = currentTarget != null ? currentTarget.name : "无目标";
            Debug.Log($"<color=#00FF00>【敌人AI状态】</color> {MyData.EnemyName} | 目标: {targetName} | 刚体速度: {rb.velocity} | 技能冷却: {moveSkillTimer:F1}");
        }
    }

    private void FindTarget()
    {
        var allPlayers = FindObjectsOfType<DamageReceiver>().Where(r => !r.isEnemy && r.CurrentHP > 0).ToList();
        if (allPlayers.Count == 0) { currentTarget = null; return; }

        switch (MyData.TargetingLogic)
        {
            case TargetingStrategy.Nearest: currentTarget = allPlayers.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).First().transform; break;
            case TargetingStrategy.MaxHPHighest: currentTarget = allPlayers.OrderByDescending(p => p.MaxHP).First().transform; break;
            case TargetingStrategy.MaxHPLowest: currentTarget = allPlayers.OrderBy(p => p.MaxHP).First().transform; break;
            case TargetingStrategy.CurrentHPHighest: currentTarget = allPlayers.OrderByDescending(p => p.CurrentHP).First().transform; break;
            case TargetingStrategy.CurrentHPLowest: currentTarget = allPlayers.OrderBy(p => p.CurrentHP).First().transform; break;
        }
    }

    private void HandleMovementAndCombat()
    {
        if (currentTarget == null || isUsingMoveSkill)
        {
            if (!isUsingMoveSkill && MyData.MoveType == EnemyMoveType.Stationary) rb.velocity = Vector2.zero;
            return;
        }

        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        float realMaxRange = MyData.GetStat(StatType.MaxRange) * distMult;
        float realMinRange = MyData.GetStat(StatType.MinRange) * distMult;
        float realSafeDist = MyData.GetStat(StatType.SafeDodgeDistance) * distMult;
        float realSpeed = MyData.GetStat(StatType.MoveSpeed) * speedMult;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        Vector2 dirToTarget = (currentTarget.position - transform.position).normalized;
        Vector2 targetVelocity = Vector2.zero;

        isFleeing = false;

        if (MyData.MoveType == EnemyMoveType.Stationary)
        {
            targetVelocity = Vector2.zero;
        }
        else if (MyData.MovementLogic == MovementStrategy.Dodge && dist < realSafeDist)
        {
            isFleeing = true;
            targetVelocity = -dirToTarget * realSpeed;
        }
        else if (dist > realMaxRange)
        {
            if (MyData.MoveType == EnemyMoveType.Normal) targetVelocity = dirToTarget * realSpeed;
            else if (moveSkillTimer <= 0f)
            {
                StartCoroutine(ExecuteSpecialMove(dirToTarget, realSpeed));
                return;
            }
        }

        rb.velocity = targetVelocity;

        if (!isFleeing && dist <= realMaxRange && dist >= realMinRange)
        {
            if (attackCooldownTimer <= 0f) PerformAttack(dirToTarget);
        }
    }

    private IEnumerator ExecuteSpecialMove(Vector2 direction, float baseSpeed)
    {
        isUsingMoveSkill = true;
        rb.velocity = Vector2.zero;

        if (MyData.MoveType == EnemyMoveType.ChargeDash)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            Color oldColor = Color.white;
            if (sr != null) { oldColor = sr.color; sr.color = new Color(1f, 0.5f, 0f); }

            float chargeT = MyData.GetStat(StatType.ChargeTime);
            yield return new WaitForSeconds(chargeT > 0 ? chargeT : 1f);

            if (sr != null) sr.color = oldColor;
            rb.velocity = direction * (baseSpeed * 3f);
            yield return new WaitForSeconds(0.5f);
            rb.velocity = Vector2.zero;
        }
        else if (MyData.MoveType == EnemyMoveType.Teleport)
        {
            yield return new WaitForSeconds(0.2f);
            float realTeleportDist = MyData.GetStat(StatType.TeleportDistance) * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
            transform.position = (Vector2)transform.position + (direction * realTeleportDist);
        }

        float cd = MyData.GetStat(StatType.SkillCooldown);
        moveSkillTimer = cd > 0 ? cd : 3f;
        isUsingMoveSkill = false;
    }

    private void PerformAttack(Vector2 attackDirection)
    {
        float atkSpeed = MyData.GetStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 50f;
        attackCooldownTimer = 100f / atkSpeed;

        float finalDmg = Random.Range(MyData.GetStat(StatType.MinDamage), MyData.GetStat(StatType.MaxDamage));
        bool isCrit = Random.value <= MyData.GetStat(StatType.CriticalChance);
        if (isCrit) finalDmg *= 1.5f;

        Debug.Log($"<color=#FF00FF>【发动攻击】</color> {MyData.EnemyName} 发动了 {MyData.DeliveryType} 攻击！判定伤害: {finalDmg}");

        if (MyData.DeliveryType == WeaponDeliveryType.Ranged && MyData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(MyData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));

            Projectile projectile = projObj.GetComponent<Projectile>();
            projectile.Fire(currentTarget, finalDmg, dummyWeaponForECA, isEnemyFire: true);
        }
        else if (MyData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext
            {
                ImpactPoint = currentTarget.position,
                PrimaryTarget = currentTarget,
                BaseDamage = finalDmg,
                SourceWeapon = dummyWeaponForECA,
                IsCriticalHit = isCrit
            };

            foreach (var action in dummyWeaponForECA.OnHitActions)
            {
                if (action != null) action.Execute(hitContext);
            }

            Rigidbody2D targetRb = currentTarget.GetComponentInParent<Rigidbody2D>();
            float force = MyData.GetStat(StatType.KnockbackForce);
            if (targetRb != null && force > 0)
            {
                targetRb.AddForce(attackDirection * force, ForceMode2D.Impulse);
            }
        }
    }

    private void ExecuteECAActions(List<ECAAction> actions, Transform target)
    {
        if (actions == null || actions.Count == 0) return;
        ECAContext context = new ECAContext { ImpactPoint = this.transform.position, PrimaryTarget = target, BaseDamage = 0f, SourceWeapon = dummyWeaponForECA };
        foreach (var action in actions) if (action != null) action.Execute(context);
    }

    // ==========================================
    // 🎨 主策专属：编辑器可视化调试系统
    // ==========================================
    private void OnDrawGizmos()
    {
        if (MyData == null) return;

        // 动态读取沙盒比例尺 (如果是运行状态)
        float distMult = 1f;
        if (Application.isPlaying && CombatSandbox.Instance != null)
        {
            distMult = CombatSandbox.Instance.DistanceMultiplier;
        }

        Vector3 center = transform.position;

        // 1. 🔴 最大攻击范围 (红圈：跨入即死！)
        float maxRange = MyData.GetStat(StatType.MaxRange) * distMult;
        if (maxRange > 0)
        {
            // 透明度调至 0.3f，多怪同框时不会瞎眼
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, maxRange);
        }

        // 2. 🟡 最小攻击范围 / 盲区 (黄圈：贴脸安全区)
        float minRange = MyData.GetStat(StatType.MinRange) * distMult;
        if (minRange > 0)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, minRange);
        }

        // 3. 🟢 安全风筝距离 (绿圈：仅躲避型 AI 显示)
        if (MyData.MovementLogic == MovementStrategy.Dodge)
        {
            float safeDist = MyData.GetStat(StatType.SafeDodgeDistance) * distMult;
            if (safeDist > 0)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(center, safeDist);
            }
        }
    }
}