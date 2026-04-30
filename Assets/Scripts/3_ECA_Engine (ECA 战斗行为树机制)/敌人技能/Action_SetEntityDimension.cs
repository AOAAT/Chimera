using UnityEngine;

[CreateAssetMenu(fileName = "Act_SetDimension", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 维度切换(加固HUD版)")]
public class Action_SetEntityDimension : ECAAction
{
    [Tooltip("True 代表潜入虚空(消失)，False 代表重返现实(出现)")]
    public bool ToOtherDimension = true;
    public GameObject PoofVFX; // 次元裂缝特效

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 1. 👇【核心修复】：使用 true 参数查找，即使 HUD 已经被禁用也能抓到它
        EntityHUD hud = context.SourceEntity.GetComponentInChildren<EntityHUD>(true);
        if (hud != null)
        {
            hud.gameObject.SetActive(!ToOtherDimension);

            // 如果是出现，额外重置一下意图，防止残留之前的进度条
            if (!ToOtherDimension) hud.HideIntent();
        }

        // 2. 视觉处理：隐藏/显示所有贴图
        // 注意：不影响 HUD，因为 HUD 现在由上面的逻辑独立控制
        SpriteRenderer[] srs = context.SourceEntity.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            // 排除 HUD 内部的图标，只控制本体
            if (hud != null && sr.transform.IsChildOf(hud.transform)) continue;
            sr.enabled = !ToOtherDimension;
        }

        // 3. 物理系统剥离 (防止碰撞和 AOE)
        Rigidbody2D rb = context.SourceEntity.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = !ToOtherDimension;

        Collider2D[] cols = context.SourceEntity.GetComponentsInChildren<Collider2D>(true);
        foreach (var col in cols)
        {
            // 排除 HUD 的点击层
            if (hud != null && col.transform.IsChildOf(hud.transform)) continue;
            col.enabled = !ToOtherDimension;
        }

        // 4. 索敌注册表同步 (让玩家转火)
        DamageReceiver dr = context.SourceEntity.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            if (ToOtherDimension)
            {
                if (dr.isEnemy) CombatDirector.ActiveEnemies.Remove(dr);
                else CombatDirector.ActivePlayerUnits.Remove(dr);
            }
            else
            {
                if (dr.isEnemy) { if (!CombatDirector.ActiveEnemies.Contains(dr)) CombatDirector.ActiveEnemies.Add(dr); }
                else { if (!CombatDirector.ActivePlayerUnits.Contains(dr)) CombatDirector.ActivePlayerUnits.Add(dr); }
            }
        }

        // 5. 特效播散
        if (PoofVFX != null) Instantiate(PoofVFX, context.SourceEntity.position, Quaternion.identity);
    }
}