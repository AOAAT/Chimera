using UnityEngine;

[CreateAssetMenu(fileName = "RandomAllyBuff", menuName = "Chimera Protocol/2. ECA 机制积木/战术 - 随机友方增益")]
public class Action_RandomAllyBuff : ECAAction
{
    public BuffDataSO BuffToApply;
    public float Range = 10f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 获取队友列表（使用之前优化过的 CombatDirector）
        var allies = context.IsEnemyFire ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;

        float realRange = CombatSandbox.GetDist(Range); // 👈 关键对齐

        var validAllies = allies.FindAll(a => a != null && a.CurrentHP > 0 &&
            Vector3.Distance(a.transform.position, context.SourceEntity.position) <= realRange);

        if (validAllies.Count > 0)
        {
            var target = validAllies[Random.Range(0, validAllies.Count)];
            BuffManager bMgr = target.GetComponent<BuffManager>();
            if (bMgr != null)
            {
                bMgr.ApplyBuff(BuffToApply, context);
                Debug.Log($"【异变肾上腺】已为 {target.name} 注射强效 Buff！");
            }
        }
    }
}