using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(ChassisSetupHelper))]
public class AssemblyStatMonitor : MonoBehaviour
{
    private ChassisSetupHelper setupHelper;
    private RuntimeChimeraData runtimeData = new RuntimeChimeraData();

    [Header("=== 全局机甲最终面板 ===")]
    public string UnitName;
    public float Final_MaxHP;
    public float Final_MaxAP;
    public float Final_TotalPowerCost;
    public float Final_TotalMass;
    public float Final_TotalEnginePower;

    [Header("=== 独立武器阵列 (战斗时将分别开火) ===")]
    [SerializeField]
    private List<WeaponDebugView> EquippedWeapons = new List<WeaponDebugView>();

    [System.Serializable]
    public struct WeaponDebugView
    {
        public string WeaponName;
        public List<StatEntry> Stats;
    }

    [Header("=== 标签库汇总 ===")]
    [SerializeField]
    private List<string> DebugTags = new List<string>();

    private void OnEnable()
    {
        setupHelper = GetComponent<ChassisSetupHelper>();
    }

    private void Update()
    {
        if (!Application.isPlaying && setupHelper != null)
        {
            CalculateTotalLoad();
        }
    }

    private void CalculateTotalLoad()
    {
        runtimeData.Assemble(setupHelper.TargetChassis, setupHelper.EquippedComponents);

        // 1. 刷新直观的全局绝对值
        UnitName = runtimeData.UnitName;
        Final_MaxHP = runtimeData.MaxHP;
        Final_MaxAP = runtimeData.MaxAP;
        Final_TotalPowerCost = runtimeData.TotalPowerCost;
        Final_TotalMass = runtimeData.TotalMass;
        Final_TotalEnginePower = runtimeData.TotalEnginePower;

        // 2. 刷新独立武器库
        EquippedWeapons.Clear();
        foreach (var weapon in runtimeData.EquippedWeapons)
        {
            var wView = new WeaponDebugView { WeaponName = weapon.WeaponName, Stats = new List<StatEntry>() };
            foreach (var kvp in weapon.WeaponStats)
            {
                wView.Stats.Add(new StatEntry { StatID = kvp.Key, Value = kvp.Value });
            }
            EquippedWeapons.Add(wView);
        }

        // 3. 刷新标签
        DebugTags.Clear();
        foreach (var tag in runtimeData.AllTags) DebugTags.Add(tag);
    }
}