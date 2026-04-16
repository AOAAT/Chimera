using UnityEngine;

[CreateAssetMenu(fileName = "MagazineControl", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 弹夹与装填控制")]
public class Action_MagazineControl : ECAAction
{
    public int MaxAmmo = 3;
    public float ReloadTime = 2.5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        var state = context.SourceWeapon.CustomStates;

        // 1. 如果正在换弹中，拦截开火！
        if (state.ContainsKey("ReloadEndTime") && Time.time < state["ReloadEndTime"])
        {
            context.ExecutionAborted = true; // 熔断，不开火！
            return;
        }

        // 2. 初始化弹夹
        if (!state.ContainsKey("CurrentAmmo")) state["CurrentAmmo"] = MaxAmmo;

        // 3. 扣除子弹
        state["CurrentAmmo"] -= 1;
        Debug.Log($"[{context.SourceWeapon.WeaponName}] 砰！剩余弹药: {state["CurrentAmmo"]}");

        // 4. 如果打空了，启动换弹！
        if (state["CurrentAmmo"] <= 0)
        {
            state["ReloadEndTime"] = Time.time + ReloadTime;
            state["CurrentAmmo"] = MaxAmmo; // 提前塞好子弹，等时间到了就能打
            Debug.Log($"<color=#FF8800>[{context.SourceWeapon.WeaponName}] 弹夹打空！开始装填，耗时 {ReloadTime} 秒！</color>");
        }
    }
}