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
        public WeaponDeliveryType DeliveryType; // 新增：显示近战/远程
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
        // 确保只有在真正运行游戏时才执行
        if (Application.isPlaying)
        {

            setupHelper.UpdateVisuals();

            // 👇👇👇 【新增：测试台自动肉体觉醒系统】 👇👇👇
            // 1. 自动注入物理刚体 (Rigidbody2D)
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;          // 俯视角，必须关掉重力！
            rb.freezeRotation = true;      // 锁定 Z 轴，防止被撞得满地打转！
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 防止高速子弹穿模

            // 2. 自动生成受击碰撞体 (BoxCollider2D)
            // 因为你的 ChassisSetupHelper 是把底盘贴图画在自己身上的，
            // Unity 极其智能：只要身上有 SpriteRenderer，AddComponent 加的 Collider 会自动完美贴合图片大小！
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();

            // 👇【核心修复】：在点火瞬间，命令车间根据图纸，把零件在游戏里真实地造出来！
            setupHelper.UpdateVisuals();

            // 1. 确保最新数据已计算完毕
            CalculateTotalLoad();

            // 2. 激活底盘的受击系统
            DamageReceiver receiver = GetComponent<DamageReceiver>();
            if (receiver != null)
            {
                receiver.Initialize(Final_MaxHP, Final_MaxAP);
            }

            ChimeraAIController aiController = GetComponent<ChimeraAIController>();
            if (aiController != null)
            {
                aiController.Initialize(runtimeData);
                Debug.Log($"【系统自检】机甲大脑已通电！当前移速: {aiController.CurrentSpeed}，耐力: {aiController.MaxStamina}");
            }

            // 3. 自动扫描并为武器“通电”！
            int weaponDataIndex = 0;
            for (int i = 0; i < setupHelper.EquippedComponents.Length; i++)
            {
                var comp = setupHelper.EquippedComponents[i];
                if (comp != null && comp.Type == ComponentType.Weapon)
                {
                    // 因为上面调用了 UpdateVisuals，这里的转轴已经变成了真实存在的物理实体！
                    Transform hinge = transform.Find($"PREVIEW_HINGE_[{i}]");
                    // ... 找到转轴 hinge ...
                    if (hinge != null)
                    {
                        WeaponModule weaponScript = hinge.gameObject.AddComponent<WeaponModule>();
                        // 👇【核心修改】：把心脏坐标和机甲的真实肉体根节点(this.transform)传给武器！
                        weaponScript.Initialize(runtimeData.EquippedWeapons[weaponDataIndex], runtimeData.LogicCenterOffset, this.transform);
                        Debug.Log($"【系统自检】插槽 [{i}] 的武器 ({comp.ComponentName}) 已通电并上线！");
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
            // 👇【UI 修复 2】：读取实体里的状态并显示在界面上！
            var wView = new WeaponDebugView
            {
                WeaponName = weapon.WeaponName,
                DeliveryType = weapon.DeliveryType,
                Stats = new List<StatEntry>()
            };
            foreach (var kvp in weapon.WeaponStats)
            {
                wView.Stats.Add(new StatEntry { StatID = kvp.Key, Value = kvp.Value });
            }
            EquippedWeapons.Add(wView);
        }

        // 3. 刷新标签
        DebugTags.Clear();
        // 👇【终极修复】：改为读取强类型的 Tags，并使用 .ToString() 翻译成文字显示！
        foreach (var tag in runtimeData.Tags)
        {
            DebugTags.Add(tag.ToString());
        }
    }
}