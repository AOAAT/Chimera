using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAccessory", menuName = "Chimera Protocol/1. 核心图纸库/配件芯片 (Accessory)")]
public class AccessoryDataSO : ScriptableObject
{
    [Header("=== 基础身份 (Identity) ===")]
    public string AccessoryID = "ACC_001";
    public string AccessoryName = "未知芯片";
    [TextArea] public string Description = "描述该芯片的逻辑功能...";
    public Sprite AccessoryIcon;
    [Range(1, 4)] public int Rarity = 1; // 1:白, 2:蓝, 3:紫, 4:橙

    [Header("=== 注入契约 (Contract) ===")]
    [Tooltip("该芯片只能安装在以下大类的零件上")]
    public List<ComponentType> AllowedTypes = new List<ComponentType> { ComponentType.Weapon };

    [Tooltip("如果是武器，是否要求特定的投递方式？(如：仅限远程)")]
    public bool LimitByDelivery = false;
    public WeaponDeliveryType RequiredDelivery = WeaponDeliveryType.Ranged;

    [Tooltip("是否要求零件必须携带特定标签？(如：仅限血肉零件)")]
    public List<SubTag> RequiredTags = new List<SubTag>();

    [Header("=== 逻辑载荷 (ECA精准注入) ===")]
    [Tooltip("当武器/组件执行开火逻辑时触发")]
    public List<ECAAction> InjectedOnFireActions = new List<ECAAction>();

    [Tooltip("当武器/组件命中目标时触发")]
    public List<ECAAction> InjectedOnHitActions = new List<ECAAction>();

    [Tooltip("每帧/周期性触发 (心跳管线)")]
    public List<ECAAction> InjectedOnTickActions = new List<ECAAction>();

    [Tooltip("装配瞬间触发 (用于初始化加成)")]
    public List<ECAAction> InjectedOnAssembleActions = new List<ECAAction>();

    [Tooltip("战斗正式打响时触发一次")]
    public List<ECAAction> InjectedOnBattleStartActions = new List<ECAAction>();
    [Header("=== 静态数值修正 (可选) ===")]
    [Tooltip("某些芯片可能直接加面板，而不通过 ECA 积木")]
    public List<StatEntry> StaticStatModifiers = new List<StatEntry>();
}