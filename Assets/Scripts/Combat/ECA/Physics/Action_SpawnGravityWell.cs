using UnityEngine;

[CreateAssetMenu(fileName = "SpawnGravityWell", menuName = "Chimera Protocol/2. ECA 机制积木/战术 - 召唤持续引力场")]
public class Action_SpawnGravityWell : ECAAction
{
    [Header("=== 实体配置 ===")]
    public GameObject GravityWellPrefab;

    [Header("=== 引力场覆盖参数 ===")]
    [Tooltip("持续时间 (秒)")]
    public float LifeTime = 3.0f;
    [Tooltip("影响半径 (数据表数值)")]
    public float Radius = 6.0f;
    [Tooltip("每帧拉扯的冲量。建议 20-50 即可，因为每帧都在加力。")]
    public float Force = 30f;

    public override void Execute(ECAContext context)
    {
        if (GravityWellPrefab == null) return;

        // 生成黑洞实体
        GameObject wellObj = Instantiate(GravityWellPrefab, context.ImpactPoint, Quaternion.identity);

        // 初始化
        GravityWellLogic logic = wellObj.GetComponent<GravityWellLogic>();
        if (logic != null)
        {
            logic.Initialize(LifeTime, Radius, Force);
        }

        // 配合打击感：黑洞张开时的震屏
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.2f, 0.15f);
    }
}