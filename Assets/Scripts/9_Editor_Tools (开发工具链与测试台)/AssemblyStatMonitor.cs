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
        public WeaponDeliveryType DeliveryType;
        public List<StatEntry> Stats;
    }

    [Header("=== 标签库汇总 ===")]
    [SerializeField]
    private List<string> DebugTags = new List<string>();

    private void OnEnable()
    {
        setupHelper = GetComponent<ChassisSetupHelper>();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            setupHelper = GetComponent<ChassisSetupHelper>();
            setupHelper.UpdateVisuals();

            // 1. 初始化物理与受击
            gameObject.layer = LayerMask.NameToLayer("Player_Body");
            Rigidbody2D rb = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            CalculateTotalLoad(); // 先算一次数据

            DamageReceiver receiver = GetComponent<DamageReceiver>() ?? gameObject.AddComponent<DamageReceiver>();
            receiver.Initialize(Final_MaxHP > 0 ? Final_MaxHP : 100f, Final_MaxAP);

            // 2. 初始化 AI
            ChimeraAIController ai = GetComponent<ChimeraAIController>() ?? gameObject.AddComponent<ChimeraAIController>();
            ai.Initialize(runtimeData);

            // 3. 【核心修复】：为测试台的所有预览武器注入 4 参数 Initialize
            int weaponDataIndex = 0;
            for (int i = 0; i < setupHelper.EquippedComponents.Length; i++)
            {
                var comp = setupHelper.EquippedComponents[i];
                if (comp != null && comp.Type == ComponentType.Weapon)
                {
                    Transform hinge = transform.Find($"PREVIEW_HINGE_[{i}]");
                    if (hinge != null)
                    {
                        WeaponModule weaponScript = hinge.gameObject.GetComponent<WeaponModule>() ?? hinge.gameObject.AddComponent<WeaponModule>();

                        // 👇 参数对齐：数据, 黑盒, 逻辑中心, 根节点
                        weaponScript.Initialize(
                            runtimeData.EquippedWeapons[weaponDataIndex],
                            runtimeData,
                            runtimeData.LogicCenterOffset,
                            this.transform
                        );
                    }
                    weaponDataIndex++;
                }
            }
        }
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
        InstancedComponent[] tempInstances = new InstancedComponent[setupHelper.EquippedComponents.Length];
        for (int i = 0; i < setupHelper.EquippedComponents.Length; i++)
        {
            if (setupHelper.EquippedComponents[i] != null)
            {
                tempInstances[i] = new InstancedComponent(setupHelper.EquippedComponents[i], 1);
            }
        }

        runtimeData.Assemble(setupHelper.TargetChassis, tempInstances);

        UnitName = runtimeData.UnitName;
        Final_MaxHP = runtimeData.MaxHP;
        Final_MaxAP = runtimeData.MaxAP;
        Final_TotalMass = runtimeData.TotalMass;
        Final_TotalEnginePower = runtimeData.TotalEnginePower;

        EquippedWeapons.Clear();
        foreach (var weapon in runtimeData.EquippedWeapons)
        {
            var wView = new WeaponDebugView { WeaponName = weapon.WeaponName, DeliveryType = weapon.DeliveryType, Stats = new List<StatEntry>() };
            foreach (var kvp in weapon.WeaponStats) wView.Stats.Add(new StatEntry { StatID = kvp.Key, Value = kvp.Value });
            EquippedWeapons.Add(wView);
        }

        DebugTags.Clear();
        foreach (var tag in runtimeData.Tags) DebugTags.Add(tag.ToString());
    }
}