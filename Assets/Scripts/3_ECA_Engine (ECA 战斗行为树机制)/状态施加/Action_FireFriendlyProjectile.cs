using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "FireFriendlyProjectile", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 发射友方增益弹")]
public class Action_FireFriendlyProjectile : ECAAction
{
    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 15f;
    public BuffDataSO BuffToApply;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null) return;

        DamageReceiver myReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        if (myReceiver == null) return;

        var allReceivers = FindObjectsOfType<DamageReceiver>();
        var allies = allReceivers.Where(r => r.isEnemy == myReceiver.isEnemy && r.CurrentHP > 0 && r.transform != context.SourceEntity).ToList();
        if (allies.Count == 0) return;

        Transform targetAlly = allies[Random.Range(0, allies.Count)].transform;

        RuntimeWeapon dummyWeapon = new RuntimeWeapon { WeaponName = "医疗发射器" };
        dummyWeapon.WeaponStats[StatType.ProjectileSpeed] = ProjectileSpeed;

        Action_ApplyBuff applyBuffAction = ScriptableObject.CreateInstance<Action_ApplyBuff>();
        applyBuffAction.BuffToApply = BuffToApply;
        dummyWeapon.OnHitActions.Add(applyBuffAction);

        GameObject projObj = Instantiate(ProjectilePrefab, context.ImpactPoint, Quaternion.identity);
        Projectile pScript = projObj.GetComponent<Projectile>();
        if (pScript != null)
        {
            // 👇【核心修正】：参数顺序完全对齐
            pScript.Fire(targetAlly, 0f, dummyWeapon, context.ChassisData, context.SourceEntity, myReceiver.isEnemy, false, 0, true);
        }
    }
}