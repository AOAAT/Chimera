using UnityEngine;
using System.Collections.Generic;

public class KineticCollisionHandler : MonoBehaviour
{
    private Rigidbody2D rb;
    private DamageReceiver myReceiver;
    private EnemyBrain myBrain; // 如果是普通敌人
    public bool IsUnstoppable { get; set; } // 霸体开关

    private float damageConversionRate;
    private List<ECAAction> onHitActions;
    private RuntimeWeapon sourceWeapon;
    private HashSet<int> hitHistory = new HashSet<int>();

    // --- 位移接管参数 ---
    private bool isOverridingMovement = false;
    private Vector2 dashVelocity;

    public void Initialize(float convRate, List<ECAAction> hitActions, RuntimeWeapon weapon)
    {
        rb = GetComponent<Rigidbody2D>();
        myReceiver = GetComponent<DamageReceiver>();
        myBrain = GetComponent<EnemyBrain>();

        this.damageConversionRate = convRate;
        this.onHitActions = hitActions;
        this.sourceWeapon = weapon;
        hitHistory.Clear();

        // --- 👇【关键修复】：起步瞬时判定 ---
        PerformImmediateScan();
    }
    private void PerformImmediateScan()
    {
        // 1. 判定攻击层级
        int targetMask = myReceiver.isEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        // 2. 在自己周围一个微小半径内扫描（模拟已经贴脸的情况）
        // 这里的 1.2f 是一个经验值，建议根据单位尺寸微调
        float scanRadius = 1.2f * CombatSandbox.GetDist(1f);
        Collider2D[] initialHits = Physics2D.OverlapCircleAll(transform.position, scanRadius, targetMask);

        foreach (var col in initialHits)
        {
            DamageReceiver victim = col.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim != myReceiver && !hitHistory.Contains(victim.gameObject.GetInstanceID()))
            {
                // 对于贴脸的目标，相对速度我们取“预期的最大冲刺速度”
                // 否则起步帧速度为 0，会导致没伤害
                float simulatedVel = 10f * CombatSandbox.GetSpeed(1f); // 给一个基础起步速度模拟

                Debug.Log($"<color=orange>【零距离侦测】</color> {gameObject.name} 撞击了贴脸的目标 {victim.name}");
                ExecuteHitLogic(victim, transform.position, Vector2.zero, simulatedVel);
            }
        }
    }

    // --- KineticCollisionHandler.cs ---

    private void ExecuteHitLogic(DamageReceiver victim, Vector3 impactPoint, Vector2 normal, float relVel)
    {
        int victimID = victim.gameObject.GetInstanceID();
        hitHistory.Add(victimID);

        float attackerMass = rb.mass;
        Rigidbody2D victimRb = victim.GetComponentInParent<Rigidbody2D>();
        float victimMass = (victimRb != null) ? victimRb.mass : 5f;

        // 1. 计算伤害
        float baseDamage = GameFormulas.CalcKineticRamDamage(attackerMass, victimMass, relVel, damageConversionRate);

        // 2. 构造并执行 ECA 管线
        ECAContext ramContext = new ECAContext
        {
            ImpactPoint = impactPoint,
            PrimaryTarget = victim.transform,
            SourceEntity = this.transform,
            BaseDamage = baseDamage,
            IsEnemyFire = myReceiver.isEnemy,
            SourceWeapon = sourceWeapon,
            ImpactVelocity = relVel,
            ImpactMass = attackerMass,
            ImpactNormal = normal
        };

        if (onHitActions != null)
        {
            foreach (var action in onHitActions) if (action != null) action.Execute(ramContext);
        }

        // --- 👇【核心重构：瞬间骤停逻辑】 ---

        // A. 速度熔断：立刻将物理速度设为 0
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f; // 防止旋转抖动

        // B. 逻辑熔断：停止 FixedUpdate 里的位移接管，防止下一帧又加速
        isOverridingMovement = false;

        // C. 产生“反作用力”小位移 (可选，增加弹开感)
        if (normal != Vector2.zero)
        {
            rb.AddForce(normal * 2f, ForceMode2D.Impulse);
        }
        // ------------------------------------------

        // 表现反馈
        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.12f);
        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(baseDamage / 100f, 0.15f);
        var chimeraAI = GetComponent<ChimeraAIController>();
        if (chimeraAI != null) chimeraAI.AbortDash();
    }

    // 最后更新 OnCollisionEnter2D 引用这个通用方法

    // 给普通单位使用的物理接管接口
    public void StartPhysicalDash(Vector2 velocity)
    {
        isOverridingMovement = true;
        dashVelocity = velocity;
        // 如果有 AI 大脑，暂时进入静默状态（利用现有的 isStaggered 逻辑）
        if (myBrain != null) myBrain.ApplyImpulse(Vector2.zero, 0.1f);
    }

    private void FixedUpdate()
    {
        // 如果是普通敌人，我们需要在 FixedUpdate 强制维持速度，防止被 AI 的 Update 覆盖
        if (isOverridingMovement && rb != null)
        {
            rb.velocity = dashVelocity;
        }
    }
    // 最后更新 OnCollisionEnter2D 引用这个通用方法
    private void OnCollisionEnter2D(Collision2D collision)
    {
        DamageReceiver victim = collision.gameObject.GetComponentInParent<DamageReceiver>();
        if (victim == null || victim == myReceiver || victim.isEnemy == myReceiver.isEnemy) return;

        if (hitHistory.Contains(victim.gameObject.GetInstanceID())) return;

        float relVel = collision.relativeVelocity.magnitude;
        if (relVel < 2.0f) return;

        ExecuteHitLogic(victim, collision.contacts[0].point, collision.contacts[0].normal, relVel);
    }

    public void Shutdown()
    {
        isOverridingMovement = false;
        Destroy(this);
    }
}