using UnityEngine;

[CreateAssetMenu(fileName = "SkyBeam", menuName = "Chimera Protocol/2. ECA 机制积木/表现 - 天火打击")]
public class Action_SkyBeam : ECAAction
{
    public GameObject BeamPrefab; // 挂载 LineRenderer 的预制体
    public float DamageRadius = 3f;
    public float Delay = 0.5f; // 预警时间

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        // 获取打击中心点（目标位置）
        Vector3 strikePos = context.PrimaryTarget.position;

        // 1. 播放打击动画（协程处理延迟）
        CombatDirector.Instance.StartCoroutine(PerformStrike(strikePos, context));
    }

    private System.Collections.IEnumerator PerformStrike(Vector3 pos, ECAContext context)
    {
        // 预警特效可在此时生成...
        yield return new UnityEngine.WaitForSeconds(Delay);

        // 生成垂直向下的光束
        if (BeamPrefab != null)
        {
            GameObject beam = Instantiate(BeamPrefab, pos + Vector3.up * 15f, Quaternion.identity);
            var lr = beam.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.SetPosition(0, pos + Vector3.up * 15f);
                lr.SetPosition(1, pos);
            }
            Destroy(beam, 0.5f);
        }
        float realRadius = CombatSandbox.GetDist(DamageRadius); // 👈 使用助手

        // 范围伤害判定
        var targets = context.IsEnemyFire ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        foreach (var target in targets.ToArray())
        {
            if (target != null && Vector3.Distance(pos, target.transform.position) <= realRadius)
            {
                target.TakeDamage(context.BaseDamage, "天火", true);
            }
        }

        ScreenEffectManager.Instance.TriggerShake(0.3f, 0.2f);
    }
}