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
            setupHelper.UpdateVisuals();

            // 1. 刚体与物理推挤层
            gameObject.layer = LayerMask.NameToLayer("Player_Body");
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 2. 脚底板物理碰撞体
            BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
            if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
            physicsCol.isTrigger = false;

            SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
            if (mainSr != null && mainSr.sprite != null)
            {
                Vector2 size = mainSr.sprite.bounds.size * setupHelper.GlobalVisualScale;
                physicsCol.size = new Vector2(size.x * 0.7f, size.y * 0.25f);
                physicsCol.offset = new Vector2(0f, -(size.y / 2f) + (physicsCol.size.y / 2f));
            }

            // 3. 全身受击 Hitbox
            Transform hitboxTrans = transform.Find("Player_Visual_Hitbox");
            GameObject hitboxObj = hitboxTrans != null ? hitboxTrans.gameObject : new GameObject("Player_Visual_Hitbox");
            if (hitboxTrans == null) hitboxObj.transform.SetParent(this.transform, false);

            hitboxObj.layer = LayerMask.NameToLayer("Player_Hitbox");
            BoxCollider2D hitboxCol = hitboxObj.GetComponent<BoxCollider2D>();
            if (hitboxCol == null) hitboxCol = hitboxObj.AddComponent<BoxCollider2D>();
            hitboxCol.isTrigger = true;

            if (mainSr != null && mainSr.sprite != null)
            {
                Vector2 size = mainSr.sprite.bounds.size * setupHelper.GlobalVisualScale;
                hitboxCol.size = size;
                hitboxCol.offset = Vector2.zero;
            }

            CalculateTotalLoad();

            DamageReceiver receiver = GetComponent<DamageReceiver>();
            if (receiver != null) receiver.Initialize(Final_MaxHP, Final_MaxAP);

            ChimeraAIController aiController = GetComponent<ChimeraAIController>();
            if (aiController != null)
            {
                aiController.Initialize(runtimeData);
                Debug.Log($"【系统自检】测试台机甲大脑已通电！当前移速: {aiController.CurrentSpeed}");
            }

            int weaponDataIndex = 0;
            for (int i = 0; i < setupHelper.EquippedComponents.Length; i++)
            {
                var comp = setupHelper.EquippedComponents[i];
                if (comp != null && comp.Type == ComponentType.Weapon)
                {
                    Transform hinge = transform.Find($"PREVIEW_HINGE_[{i}]");
                    if (hinge != null)
                    {
                        WeaponModule weaponScript = hinge.gameObject.AddComponent<WeaponModule>();
                        weaponScript.Initialize(runtimeData.EquippedWeapons[weaponDataIndex], runtimeData.LogicCenterOffset, this.transform);
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
        // 👇【核心修复 2】：为了兼容测试台的 SO 数组，我们在外层包一个 1级的 Instance 壳！
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
        Final_TotalPowerCost = runtimeData.TotalPowerCost;
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