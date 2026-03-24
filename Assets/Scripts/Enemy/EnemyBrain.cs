using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// 运行时的技能状态追踪器
public class RuntimeEnemySkill
{
    public EnemySkillSO SkillData;
    public float CurrentCooldown;
    public RuntimeWeapon DummyWeapon; // 专门用来兼容 ECA 积木的伪装层
}

[RequireComponent(typeof(DamageReceiver))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    public EnemyDataSO MyData;

    private DamageReceiver myReceiver;
    private Rigidbody2D rb;
    private Transform currentTarget;

    // 👇【新增】：怪物的内存技能库
    private List<RuntimeEnemySkill> runtimeSkills = new List<RuntimeEnemySkill>();

    private float lastFrameHP;
    private bool isDead = false;

    private void Start()
    {
        if (MyData == null) { enabled = false; return; }

        myReceiver = GetComponent<DamageReceiver>();
        rb = GetComponent<Rigidbody2D>();
        lastFrameHP = myReceiver.CurrentHP;

        // 👇 1. 把图纸里的技能，实例化到大脑内存里，并伪装成机甲武器供 ECA 使用！
        foreach (var skillSO in MyData.Skills)
        {
            if (skillSO == null) continue;
            var rSkill = new RuntimeEnemySkill { SkillData = skillSO, CurrentCooldown = 0f };

            // 捏造伪装武器
            rSkill.DummyWeapon = new RuntimeWeapon
            {
                WeaponName = skillSO.SkillName,
                DeliveryType = skillSO.DeliveryType,
                ProjectilePrefab = skillSO.ProjectilePrefab,
                SourceSO = null
            };
            rSkill.DummyWeapon.WeaponStats[StatType.MaxDamage] = skillSO.MaxDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.MinDamage] = skillSO.MinDamage;
            rSkill.DummyWeapon.WeaponStats[StatType.ProjectileSpeed] = skillSO.ProjectileSpeed;
            rSkill.DummyWeapon.WeaponStats[StatType.AttackSpeed] = skillSO.AttackSpeed;
            rSkill.DummyWeapon.OnHitActions.AddRange(skillSO.OnHitActions);

            runtimeSkills.Add(rSkill);
        }

        ExecuteECAActions(MyData.OnSpawnActions, this.transform, null);
    }

    private void Update()
    {
        if (isDead) return;

        if (myReceiver.CurrentHP < lastFrameHP)
        {
            ExecuteECAActions(MyData.OnTakeDamageActions, this.transform, null);
            lastFrameHP = myReceiver.CurrentHP;
        }

        if (myReceiver.CurrentHP <= 0)
        {
            isDead = true;
            rb.velocity = Vector2.zero;
            ExecuteECAActions(MyData.OnDeathActions, this.transform, null);
            return;
        }

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 👇 2. 所有技能冷却时间转起来！
        foreach (var skill in runtimeSkills)
        {
            if (skill.CurrentCooldown > 0) skill.CurrentCooldown -= Time.deltaTime;
        }

        FindTarget();
        HandleMovementAndCombat();
    }

    private void FindTarget()
    {
        var allPlayers = FindObjectsOfType<DamageReceiver>().Where(r => !r.isEnemy && r.CurrentHP > 0).ToList();
        if (allPlayers.Count == 0) { currentTarget = null; return; }

        switch (MyData.TargetingLogic)
        {
            case TargetingStrategy.Nearest: currentTarget = allPlayers.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).First().transform; break;
            case TargetingStrategy.MaxHPHighest: currentTarget = allPlayers.OrderByDescending(p => p.MaxHP).First().transform; break;
            // (其他逻辑同理，此处省略以保持代码紧凑)
            default: currentTarget = allPlayers.OrderBy(p => Vector3.Distance(transform.position, p.transform.position)).First().transform; break;
        }
    }

    private void HandleMovementAndCombat()
    {
        if (currentTarget == null || MyData.MoveType == EnemyMoveType.Stationary)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        Vector2 dirToTarget = (currentTarget.position - transform.position).normalized;

        // 👇 3. 动态寻找最大交战距离 (为了让 AI 知道什么时候该停车)
        float maxEngagementRange = 0f;
        if (runtimeSkills.Count > 0) maxEngagementRange = runtimeSkills.Max(s => s.SkillData.MaxRange) * distMult;

        bool isFleeing = false;
        Vector2 targetVelocity = Vector2.zero;

        if (MyData.MovementLogic == MovementStrategy.Dodge && dist < MyData.SafeDodgeDistance * distMult)
        {
            isFleeing = true;
            targetVelocity = -dirToTarget * (MyData.GetStat(StatType.MoveSpeed) * speedMult);
        }
        else if (dist > maxEngagementRange)
        {
            // 还没进入任何一个技能的射程，继续冲！
            targetVelocity = dirToTarget * (MyData.GetStat(StatType.MoveSpeed) * speedMult);
        }

        rb.velocity = targetVelocity;

        // 👇 4. 终极轮盘赌：筛选出当前可以释放的技能！
        if (!isFleeing)
        {
            var availableSkills = runtimeSkills.Where(s =>
                s.CurrentCooldown <= 0 &&
                dist <= (s.SkillData.MaxRange * distMult) &&
                dist >= (s.SkillData.MinRange * distMult)
            ).ToList();

            if (availableSkills.Count > 0)
            {
                // 根据权重进行抽卡！
                float totalWeight = availableSkills.Sum(s => s.SkillData.SelectionWeight);
                float roll = Random.Range(0, totalWeight);
                RuntimeEnemySkill chosenSkill = null;

                foreach (var skill in availableSkills)
                {
                    roll -= skill.SkillData.SelectionWeight;
                    if (roll <= 0) { chosenSkill = skill; break; }
                }

                if (chosenSkill != null) PerformAttack(chosenSkill, dirToTarget);
            }
        }
    }

    private void PerformAttack(RuntimeEnemySkill rSkill, Vector2 attackDirection)
    {
        var skillData = rSkill.SkillData;

        float currentAtkSpeed = rSkill.DummyWeapon.GetStat(StatType.AttackSpeed);
        if (currentAtkSpeed <= 0) currentAtkSpeed = 50f;
        rSkill.CurrentCooldown = 100f / currentAtkSpeed;

        float finalDmg = Random.Range(skillData.MinDamage, skillData.MaxDamage);
        bool isCrit = Random.value <= skillData.CriticalChance;
        if (isCrit) finalDmg *= 1.5f;

        // 👇【新增评估日志】：敌人技能抽卡与开火详情
        string critLog = isCrit ? "<color=#FFD700><b>(暴击!)</b></color>" : "";
        Debug.Log($"<color=#FF00FF>【敌人施法】</color> [{MyData.EnemyName}] 对 [{currentTarget.name}] 释放了 [{skillData.SkillName}]！| 判定伤害: {finalDmg:F1} {critLog}");

        ECAContext fireContext = new ECAContext { ImpactPoint = transform.position, PrimaryTarget = currentTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
        foreach (var action in skillData.OnFireActions) if (action != null) action.Execute(fireContext);

        if (skillData.DeliveryType == WeaponDeliveryType.Ranged && skillData.ProjectilePrefab != null)
        {
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            GameObject projObj = Instantiate(skillData.ProjectilePrefab, transform.position, Quaternion.AngleAxis(angle, Vector3.forward));
            Projectile projectile = projObj.GetComponent<Projectile>();
            projectile.Fire(currentTarget, finalDmg, rSkill.DummyWeapon, isEnemy: true);
        }
        else if (skillData.DeliveryType == WeaponDeliveryType.Melee)
        {
            ECAContext hitContext = new ECAContext { ImpactPoint = currentTarget.position, PrimaryTarget = currentTarget, BaseDamage = finalDmg, SourceWeapon = rSkill.DummyWeapon, IsCriticalHit = isCrit, IsEnemyFire = true };
            foreach (var action in skillData.OnHitActions) if (action != null) action.Execute(hitContext);

            Rigidbody2D targetRb = currentTarget.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null && skillData.KnockbackForce > 0)
            {
                targetRb.AddForce(attackDirection * skillData.KnockbackForce, ForceMode2D.Impulse);
            }
        }
    }
    private void ExecuteECAActions(List<ECAAction> actions, Transform target, RuntimeWeapon dummyWeapon)
    {
        if (actions == null || actions.Count == 0) return;
        ECAContext context = new ECAContext { ImpactPoint = this.transform.position, PrimaryTarget = target, BaseDamage = 0f, SourceWeapon = dummyWeapon };
        foreach (var action in actions) if (action != null) action.Execute(context);
    }
    private void OnDrawGizmos()
    {
        if (MyData == null) return;

        float distMult = 1f;
        if (Application.isPlaying && CombatSandbox.Instance != null)
        {
            distMult = CombatSandbox.Instance.DistanceMultiplier;
        }

        Vector3 center = transform.position;

        // 1. 🟢 躲避型 AI 的安全风筝距离
        if (MyData.MovementLogic == MovementStrategy.Dodge)
        {
            float safeDist = MyData.SafeDodgeDistance * distMult;
            if (safeDist > 0)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(center, safeDist);
            }
        }

        // 2. 🔮 遍历绘制所有技能池的射程圈！
        if (MyData.Skills != null && MyData.Skills.Count > 0)
        {
            foreach (var skill in MyData.Skills)
            {
                if (skill == null) continue;

                // 最大射程 (蓝色半透明：可开火区)
                float maxRange = skill.MaxRange * distMult;
                if (maxRange > 0)
                {
                    Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f); // 淡蓝色，多技能重叠时不刺眼
                    Gizmos.DrawWireSphere(center, maxRange);
                }

                // 最小盲区 (红色半透明：贴脸无效区)
                float minRange = skill.MinRange * distMult;
                if (minRange > 0)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                    Gizmos.DrawWireSphere(center, minRange);
                }
            }
        }
    }
}