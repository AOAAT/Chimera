using UnityEngine;

[CreateAssetMenu(fileName = "ConsumeSelfHP", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 扣除自身生命值")]
public class Action_ConsumeSelfHP : ECAAction
{
    [Tooltip("每次触发扣除的绝对值")]
    public float HPToConsume = 5f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        DamageReceiver dr = context.SourceEntity.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            // 强制扣血，但不触发“受击”积木，避免死循环
            dr.CurrentHP -= HPToConsume;

            // 视觉反馈：在自己身上弹出一个细小的红色数字
            if (DamagePopupManager.Instance != null)
                DamagePopupManager.Instance.SpawnPopup(context.SourceEntity.position + Vector3.up, HPToConsume, false, false, false, true);

            if (dr.CurrentHP <= 0)
            {
                Debug.LogWarning("【系统提示】指挥官，您的机甲因过度献祭而自毁！");
            }
        }
    }
}