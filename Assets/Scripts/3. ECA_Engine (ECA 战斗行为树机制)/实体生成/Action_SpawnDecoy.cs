// --- START OF FILE Action_SpawnDecoy.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnDecoy", menuName = "Chimera Protocol/2. ECA 机制积木/战术 - 召唤诱饵掩体 (Spawn Decoy)")]
public class Action_SpawnDecoy : ECAAction
{
    [Tooltip("拖入一个挂有 DamageReceiver 的障碍物/假人预制体")]
    public GameObject DecoyPrefab;
    public float DecoyHP = 500f;
    [Tooltip("诱饵存留时间")]
    public float Lifespan = 5f;

    public override void Execute(ECAContext context)
    {
        if (DecoyPrefab == null || context.ImpactPoint == null) return;

        // 在目标点(或机甲脚下)生成一个诱饵
        GameObject decoy = Instantiate(DecoyPrefab, context.ImpactPoint, Quaternion.identity);

        DamageReceiver dr = decoy.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            dr.Initialize(DecoyHP, 0);
            // 极其关键：诱饵的阵营必须和施法者一致！这样怪物才会去打它！
            dr.isEnemy = context.IsEnemyFire;
        }

        Destroy(decoy, Lifespan);
    }
}