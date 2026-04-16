using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "FireFriendlyProjectile", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 发射友方增益弹")]
public class Action_FireFriendlyProjectile : ECAAction
{
    [Tooltip("飞向队友的子弹预制体 (如一根绿色的针管)")]
    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 15f;
    [Tooltip("子弹命中队友后，挂载什么 Buff？")]
    public BuffDataSO BuffToApply;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null) return;

        DamageReceiver myReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        if (myReceiver == null) return;

        // 1. 找队友 (必须是活着的同阵营，且不能是自己)
        var allReceivers = FindObjectsOfType<DamageReceiver>();
        var allies = allReceivers.Where(r => r.isEnemy == myReceiver.isEnemy && r.CurrentHP > 0 && r.transform != context.SourceEntity).ToList();

        if (allies.Count == 0) return; // 没队友就不射了

        // 2. 随机抽一个倒霉的幸运队友
        Transform targetAlly = allies[Random.Range(0, allies.Count)].transform;

        // 3. 捏造一个“虚假武器”作为子弹的信使
        RuntimeWeapon dummyWeapon = new RuntimeWeapon { WeaponName = "医疗发射器" };
        dummyWeapon.WeaponStats[StatType.ProjectileSpeed] = ProjectileSpeed;

        // 4. 让子弹命中时，执行“施加 Buff” 的动作！
        Action_ApplyBuff applyBuffAction = ScriptableObject.CreateInstance<Action_ApplyBuff>();
        applyBuffAction.BuffToApply = BuffToApply;
        dummyWeapon.OnHitActions.Add(applyBuffAction);

        // 5. 开火发射！(设置 targetAllies = true)
        GameObject projObj = Instantiate(ProjectilePrefab, context.ImpactPoint, Quaternion.identity);
        projObj.GetComponent<Projectile>().Fire(targetAlly, 0f, dummyWeapon, myReceiver.isEnemy, false, true);
    }
}