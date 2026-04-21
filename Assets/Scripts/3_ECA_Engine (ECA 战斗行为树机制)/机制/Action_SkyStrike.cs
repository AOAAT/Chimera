using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "SkyStrike", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 天火打击")]
public class Action_SkyStrike : ECAAction
{
    [Header("=== 视觉表现 ===")]
    public GameObject WarningVFX;
    public GameObject BeamPrefab;
    public GameObject ExplosionVFX;

    [Header("=== 逻辑参数 ===")]
    public float Delay = 0.6f;
    public float DamageRadius = 3f;

    // 👇【核心新增】：允许你在 Inspector 里切换伤害类型
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;
        Vector3 targetPos = context.PrimaryTarget.position;
        CombatDirector.Instance.StartCoroutine(ExecuteSkyStrike(targetPos, context));
    }

    private IEnumerator ExecuteSkyStrike(Vector3 impactPos, ECAContext context)
    {
        // 1. 地面预警
        if (WarningVFX != null)
        {
            GameObject warning = Instantiate(WarningVFX, impactPos, Quaternion.identity);
            if (warning != null) Destroy(warning, Delay);
        }

        // 2. 光束俯冲
        if (BeamPrefab != null)
        {
            Vector3 spawnPos = impactPos + Vector3.up * 15f;
            GameObject beam = Instantiate(BeamPrefab, spawnPos, Quaternion.Euler(90, 0, 0));

            // 👇【核心加固】：静默处理
            // 如果预制体带了子弹脚本或碰撞盒，强行关掉它们，防止它们在下落时乱动或自爆
            var proj = beam.GetComponent<Projectile>();
            if (proj != null) proj.enabled = false;
            var col = beam.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            float elapsed = 0;
            while (elapsed < Delay)
            {
                if (beam == null)
                {
                    // 如果依然被销毁，记录警告并跳出
                    Debug.LogWarning("【天火异常】光束预制体依然在下落中被销毁了。请检查预制体是否带了 DestroySelf 类脚本。");
                    yield break;
                }

                elapsed += Time.deltaTime;
                beam.transform.position = Vector3.Lerp(spawnPos, impactPos, elapsed / Delay);
                yield return null;
            }
            if (beam != null) Destroy(beam);
        }
        else yield return new WaitForSeconds(Delay);

        // 3. 落地结算
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) yield break;
        if (ExplosionVFX != null) Instantiate(ExplosionVFX, impactPos, Quaternion.identity);

        float realRadius = CombatSandbox.GetDist(DamageRadius);

        // 使用我们优化过的静态列表，根据 context 判定敌我
        var targets = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        // 注意：这里需要复制一份列表，防止在遍历时因为单位死亡移出列表导致报错
        foreach (var t in targets.ToList())
        {
            if (t != null && t.CurrentHP > 0 && Vector3.Distance(impactPos, t.transform.position) <= realRadius)
            {
                // 👇【修复点】：传入 context 里的临时倍率，并使用配置好的伤害类型
                t.TakeDamage(context.BaseDamage * context.TemporaryDamageModifier, "天火", IsTrueDamage);
            }
        }

        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.4f, 0.2f);
    }
}