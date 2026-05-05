using System;
using Unity.VisualScripting;
using UnityEngine;

public enum UISoundType
{
    Generic_Hover,      // 通用悬停
    Generic_Click,      // 通用点击/确认
    Generic_Cancel,     // 取消/返回
    Generic_Warning,    // 错误/警告

    Mech_Attach,        // 零件扣入插槽
    Mech_Detach,        // 零件卸载
    Mech_PowerOn,       // 机甲通电

    Loot_BoxOpen,       // 盲盒开启
    Loot_ItemEject,     // 零件弹出
    Loot_ScrapIn,       // 废料入账

    // --- 👇【方案A：新增特殊事件类型】 ---
    Combat_Victory,     // 战斗胜利（激昂、正向）
    Combat_Failure,     // 任务失败（沉重、负面）
    UI_UpgradeSuccess,  // 零件强化成功（火花四溅、突破感）
    UI_RareItemGet,     // 获得传说/史诗零件（金光闪闪的感觉）
    Map_NodeSelect      // 地图节点选择（雷达扫描感）
}

[CreateAssetMenu(fileName = "UISoundAtlas", menuName = "Chimera Protocol/Audio/UI Sound Atlas")]
public class UISoundAtlasSO : ScriptableObject
{
    [Serializable]
    public struct SoundMapping
    {
        public UISoundType Type;
        public AudioProfileSO Profile;
    }

    public SoundMapping[] Mappings;

    public AudioProfileSO GetProfile(UISoundType type)
    {
        foreach (var mapping in Mappings)
        {
            if (mapping.Type == type) return mapping.Profile;
        }
        return null;
    }
}