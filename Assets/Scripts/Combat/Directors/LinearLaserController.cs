using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class LinearLaserController : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private ECAContext originalContext;
    private LinearLaserConfig config;
    private List<ECAAction> onHitActions;

    private float totalDuration;
    private float timer;
    private float tickTimer;
    private bool isLocked = false;
    private bool isFired = false;
    private bool singleHitDealt = false;
    private Vector2 lockDirection;
    private Transform shooter;
    private float finalRange;
    public bool IsUnstoppable => config != null && config.IsUnstoppable;
    public void Initialize(ECAContext context, LinearLaserConfig config, List<ECAAction> hitActions, float duration)
    {
        this.originalContext = context;
        this.config = config;
        this.onHitActions = hitActions;
        this.totalDuration = duration;
        this.shooter = context.SourceEntity;

        // --- 射程计算逻辑 ---
        float rawRange = 0f;
        if (context.SourceWeapon != null)
        {
            rawRange = context.SourceWeapon.GetStat(StatType.MaxRange);
        }

        // 优先级：武器配的 MaxRange > Config 里的默认 MaxDistance
        if (rawRange <= 0.1f)
        {
            rawRange = config.MaxDistance;
        }

        this.finalRange = CombatSandbox.GetDist(rawRange);

        // --- 初始化 LineRenderer ---
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = config.SubdivisionPoints;
        lineRenderer.startWidth = config.BeamWidth * 0.5f;
        lineRenderer.endWidth = config.BeamWidth * 0.5f;
        lineRenderer.startColor = config.TrackingColor;
        lineRenderer.endColor = config.TrackingColor;
        timer = 0;
    }

    private void Update()
    {
        if (shooter == null)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;
        float progress = timer / totalDuration;

        if (progress < config.TrackingRatio)
        {
            UpdateTracking();
        }
        else if (progress < (config.TrackingRatio + config.LockingRatio))
        {
            UpdateLocking();
        }
        else if (progress < 1.0f)
        {
            UpdateFiring(progress);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateTracking()
    {
        transform.position = shooter.position;
        if (originalContext.PrimaryTarget != null)
        {
            lockDirection = (originalContext.PrimaryTarget.position - shooter.position).normalized;
        }
        RenderProceduralLine(lockDirection, 0f);
    }

    private void UpdateLocking()
    {
        if (!isLocked)
        {
            isLocked = true;
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
        }
        transform.position = shooter.position;
        Vector2 jitterDir = lockDirection + (Random.insideUnitCircle * config.JitterAmplitude);
        RenderProceduralLine(jitterDir.normalized, 0f);
    }

    private void UpdateFiring(float progress)
    {
        if (!isFired)
        {
            isFired = true;
            lineRenderer.startWidth = config.BeamWidth;
            lineRenderer.endWidth = config.BeamWidth;
            lineRenderer.startColor = config.FiringColor;
            lineRenderer.endColor = config.FiringColor;
        }
        transform.position = shooter.position;
        RenderProceduralLine(lockDirection, config.NoiseIntensity);

        if (config.IsSustainedDamage)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= (1f / config.TickRate))
            {
                tickTimer = 0;
                float firingTime = totalDuration * config.FiringRatio;
                float totalTicks = config.TickRate * firingTime;
                ExecuteCasting(originalContext.BaseDamage / Mathf.Max(1f, totalTicks));
            }
        }
        else
        {
            if (!singleHitDealt)
            {
                ExecuteCasting(originalContext.BaseDamage);
                singleHitDealt = true;
                if (ScreenEffectManager.Instance != null)
                {
                    ScreenEffectManager.Instance.TriggerShake(0.4f, 0.15f);
                }
            }
        }
    }

    private void ExecuteCasting(float damageToApply)
    {
        // 1. 扫描射线路径上的敌人
        int mask = originalContext.IsEnemyFire ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
        RaycastHit2D[] hits = Physics2D.CircleCastAll(shooter.position, config.BeamWidth, lockDirection, finalRange, mask);

        var sortedHits = hits.OrderBy(h => h.distance).ToList();
        int piercingCount = 0;

        foreach (var hit in sortedHits)
        {
            DamageReceiver dr = hit.collider.GetComponentInParent<DamageReceiver>();
            if (dr != null && piercingCount < config.MaxTargets)
            {
                // 🌟 【ECA 2.0 核心】：不再在这里 TakeDamage
                // 而是为激光照射到的每一个目标，分发一次命中管线

                // 构造激光命中快照
                ECAContext laserHitCtx = new ECAContext
                {
                    SourceEntity = shooter,
                    PrimaryTarget = dr.transform,
                    ImpactPoint = hit.point,
                    SourceWeapon = originalContext.SourceWeapon,
                    BaseDamage = damageToApply * Mathf.Pow(config.PiercingDecay, piercingCount),
                    IsCriticalHit = originalContext.IsCriticalHit,
                    IsEnemyFire = originalContext.IsEnemyFire,
                    TemporaryDamageModifier = originalContext.TemporaryDamageModifier // 继承激光原本的加成
                };

                // 呼叫武器的 OnHit 管线 (此时会自动触发 DealDamage 和 附魔效果)
                originalContext.SourceWeapon.TriggerHitPipeline(dr.transform, hit.point, laserHitCtx);

                piercingCount++;
            }
        }
    }
    private void RenderProceduralLine(Vector2 direction, float noise)
    {
        Vector3 start = shooter.position;
        Vector3 end = start + (Vector3)direction * finalRange;
        Vector3 upNormal = Vector3.Cross(direction, Vector3.forward).normalized;

        for (int i = 0; i < config.SubdivisionPoints; i++)
        {
            float t = i / (float)Mathf.Max(1, config.SubdivisionPoints - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);

            if (noise > 0)
            {
                float n = Mathf.PerlinNoise(Time.time * 25f, t * 10f) - 0.5f;
                pos += upNormal * n * noise;
            }
            lineRenderer.SetPosition(i, pos);
        }
    }
}