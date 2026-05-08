using UnityEngine;

[CreateAssetMenu(fileName = "MagazineControl", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 弹夹与装填控制")]
public class Action_MagazineControl : ECAAction
{
    [Header("=== 弹夹规格 ===")]
    [Tooltip("弹夹总容量 (例如：3 代表可以打 3 发再换弹)")]
    public int MaxAmmo = 3;

    [Tooltip("换弹持续时间 (秒)")]
    public float ReloadTime = 2.5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        var states = context.SourceWeapon.CustomStates;

        // --- 1. 换弹期判定 (优先级最高) ---
        if (states.TryGetValue("ReloadEndTime", out float endTime))
        {
            if (Time.time < endTime)
            {
                // 还在读秒中，强行熔断，本次不开火
                context.ExecutionAborted = true;
                float remaining = endTime - Time.time;
                // Debug.Log($"<color=yellow>[{context.SourceWeapon.WeaponName}] 正在装填中... 剩余 {remaining:F1}s</color>");
                return;
            }
        }

        // --- 2. 状态初始化 (首次开火或重新战斗) ---
        if (!states.ContainsKey("CurrentAmmo"))
        {
            states["CurrentAmmo"] = MaxAmmo;
        }

        // --- 3. 弹药余量检查 ---
        float currentAmmo = states["CurrentAmmo"];

        if (currentAmmo > 0)
        {
            // A. 【消耗弹药】：扣除一发，并允许本次开火继续向下执行
            states["CurrentAmmo"] = currentAmmo - 1;

          

            // B. 【临界点检查】：如果打完这发正好空了，立刻启动换弹倒计时
            if (states["CurrentAmmo"] <= 0)
            {
                states["ReloadEndTime"] = Time.time + ReloadTime;

                // 【核心修复】：立即重置弹药数，这样读秒结束后，第一步判定就能通过
                states["CurrentAmmo"] = MaxAmmo;

                
            }
        }
        else
        {
            // C. 【保险兜底】：如果弹药已经是 0 (极少发生)，强制进入换弹并拦截
            states["ReloadEndTime"] = Time.time + ReloadTime;
            states["CurrentAmmo"] = MaxAmmo;
            context.ExecutionAborted = true;
        }
    }
}