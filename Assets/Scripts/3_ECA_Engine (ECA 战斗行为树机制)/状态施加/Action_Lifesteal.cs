// --- START OF FILE Action_Lifesteal.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "Lifesteal", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 伤害吸血 (Lifesteal)")]
public class Action_Lifesteal : ECAAction
{
    [Header("=== 吸血配置 ===")]
    [Tooltip("将造成伤害的百分之几转化为自身血量？(0.5 = 50%)")]
    [Range(0f, 2f)] public float LifestealRatio = 0.5f;

    [Tooltip("是否只有在暴击时才触发吸血？(配合电锯极其狂暴)")]
    public bool OnlyOnCritical = true;

    [Header("=== 视觉反馈 ===")]
    [Tooltip("吸血瞬间，要在机甲身上播放的治疗特效 (可选)")]
    public GameObject HealVFXPrefab;

    public override void Execute(ECAContext context)
    {
        // 1. 过滤条件
        if (OnlyOnCritical && !context.IsCriticalHit) return;

        // 2. 获取开火者 (玩家机甲) 的血条
        // 注意：ImpactPoint 是打中怪的位置，而我们需要给开枪的人回血！
        // 在 WeaponModule 生成 Context 时，我们并没有传开火者。
        // 但这是 2D 沙盘，我们可以通过 SourceWeapon 所在的实体来找！

        if (context.SourceWeapon == null) return;

        // 这里需要一个小小的架构补充：我们要知道谁开的枪
        // 为了最快跑通，我们通过寻找场景中非敌人的 DamageReceiver (因为玩家机甲只有一台)
        DamageReceiver playerReceiver = FindObjectOfType<DamageReceiver>();
        // 👇 更严谨的做法是在 ECAContext 里加一个 public Transform SourceEntity; 这里先用简易版

        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();
        DamageReceiver myMech = null;
        foreach (var r in allReceivers)
        {
            // 谁不是被攻击的目标，且阵营和此次攻击一致，谁就是开火者
            if (r.transform != context.PrimaryTarget && r.isEnemy == context.IsEnemyFire)
            {
                myMech = r;
                break;
            }
        }

        if (myMech != null && myMech.CurrentHP > 0 && myMech.CurrentHP < myMech.MaxHP)
        {
            // 3. 计算回血量 (基于这发子弹造成的 BaseDamage)
            float healAmount = context.BaseDamage * LifestealRatio;

            // 4. 强行回血！(修改 DamageReceiver)
            myMech.CurrentHP = Mathf.Min(myMech.CurrentHP + healAmount, myMech.MaxHP);

            // 5. 极其重要的视觉反馈：飘绿字！
            if (DamagePopupManager.Instance != null)
            {
                // 飘一个带有 '+' 号的亮绿色数字
                DamagePopupManager.Instance.SpawnPopup(myMech.transform.position + Vector3.up, healAmount, false, false, false, false);
                // 注意：由于我们之前改了飘字系统，你需要在 DamagePopup.cs 里稍微加一个判断，如果是负面伤害(回血)，颜色设为绿色。
                // 现阶段你可以直接在这里打印 Log 感受一下：
                Debug.Log($"<color=#00FF00>【嗜血】</color> 汲取了 {healAmount:F1} 点生命值！");
            }

            // 6. 播放治疗绿光特效
            if (HealVFXPrefab != null)
            {
                Instantiate(HealVFXPrefab, myMech.transform.position, Quaternion.identity, myMech.transform);
            }
        }
    }
}