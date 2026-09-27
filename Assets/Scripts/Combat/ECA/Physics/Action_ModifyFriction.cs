// --- START OF FILE Action_ModifyFriction.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "ModifyFriction", menuName = "Chimera Protocol/2. ECA 机制积木/物理 - 改变摩擦力 (Modify Friction)")]
public class Action_ModifyFriction : ECAAction
{
    [Tooltip("修改刚体的空气阻力/摩擦力。默认是 5，填 -4 会让它变成 1，变成滑冰鞋！")]
    public float DragModifier = -4f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        Rigidbody2D rb = context.SourceEntity.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.drag = Mathf.Max(0f, rb.drag + DragModifier);
            Debug.Log($"【底盘改装】机甲摩擦力变更为 {rb.drag}！");
        }
    }
}