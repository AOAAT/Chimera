using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using System.Linq;

public class MechUnit2D : MonoBehaviour
{
    private SavedUnitProfile bindedData;
    private RuntimeChimeraData cachedCombatData;

    [Header("=== 核心引用 ===")]
    public Transform VisualRoot;

    [Header("=== 2D 排序层控制 ===")]
    public string SortingLayerName = "Entities";
    public int BaseSortingOrder = 0;

    [Header("=== 战场视觉与物理缩放 ===")]
    [Range(0.1f, 5f)]
    public float GlobalBattleScale = 1.0f;

    private Rigidbody2D rb;
    private BoxCollider2D physicsCol;
    private bool isDragging = false;
    private Vector3 dragStartPos;

    private void Awake()
    {
        if (VisualRoot == null)
        {
            GameObject visualRootObj = new GameObject("UnitVisualContainer_2D");
            visualRootObj.transform.SetParent(this.transform, false);
            VisualRoot = visualRootObj.transform;
        }
    }

    // ==========================================
    // 🚀 入口 A：玩家机甲初始化
    // ==========================================
    public void InitUnitData(SavedUnitProfile data)
    {
        int totalSockets = data.ChassisData.Sockets.Count;
        InstancedComponent[] tempInstances = new InstancedComponent[totalSockets];

        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (compInstance != null && slotIdx < totalSockets)
            {
                tempInstances[slotIdx] = compInstance;
            }
        }
        FullSetup(data, tempInstances, false);
    }

    // ==========================================
    // 🚀 入口 B：精英敌人初始化
    // ==========================================
    public void InitAsEliteEnemy(EnemyDataSO enemySO)
    {
        SavedUnitProfile eliteProfile = new SavedUnitProfile(new InstancedChassis(enemySO.Chassis), enemySO.EnemyName);
        int totalSockets = enemySO.Chassis.Sockets.Count;
        InstancedComponent[] comps = new InstancedComponent[totalSockets];

        for (int i = 0; i < enemySO.Components.Count; i++)
        {
            if (i >= totalSockets || enemySO.Components[i] == null) continue;
            comps[i] = new InstancedComponent(enemySO.Components[i], enemySO.EliteComponentLevel);
            eliteProfile.SlotIndices.Add(i);
            eliteProfile.EquippedComponentIDs.Add("ELITE_TEMP_" + i);
        }
        FullSetup(eliteProfile, comps, true, enemySO);
    }

    // ==========================================
    // 🛠️ 核心驱动：暴力初始化流程 (绝对防御版)
    // ==========================================
    private void FullSetup(SavedUnitProfile data, InstancedComponent[] comps, bool isEnemy, EnemyDataSO enemySO = null)
    {
        // 1. 根节点组件强制安装
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        physicsCol = GetComponent<BoxCollider2D>();
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();

        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();

        // 2. 基础属性锁定
        this.bindedData = data;
        this.name = (isEnemy ? "[ELITE] " : "[UNIT] ") + data.UnitName;
        foreach (Transform child in VisualRoot) Destroy(child.gameObject);

        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        transform.localScale = Vector3.one * GlobalBattleScale;
        gameObject.layer = LayerMask.NameToLayer(isEnemy ? "Enemy_Body" : "Player_Body");

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        physicsCol.isTrigger = false;
        sg.sortingLayerName = SortingLayerName;

        // 3. 构建底盘 (Visual_ChassisBase)
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        chassisObj.layer = LayerMask.NameToLayer(isEnemy ? "Enemy_Hitbox" : "Player_Hitbox");

        // 暴力安装底盘核心组件
        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>(); // 👈 直接 Add，因为是新生成的 GameObject

        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        hitboxCol.isTrigger = true;
        Vector2 spriteSize = chassisSR.sprite.bounds.size;
        hitboxCol.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.9f);

        // 4. 适配脚底物理碰撞板尺寸 (Root 上的 Collider)
        physicsCol.size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        physicsCol.offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.size.y / 2f));

        // 5. 动态深度排序注入
        DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>();
        if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
        sorter.YOffset = -(spriteSize.y / 2f);

        // 6. 零件挂载循环
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            var comp = comps[i];
            var slotDef = data.ChassisData.Sockets[i];

            string typeTag = (comp.BaseData.Type == ComponentType.Movement) ? "_MovementType" : "";
            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}{typeTag}");
            slotObj.layer = chassisObj.layer;
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.layer = chassisObj.layer;
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.layer = chassisObj.layer;
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer cpSR = visObj.AddComponent<SpriteRenderer>();
            cpSR.sprite = comp.BaseData.ComponentIcon;
            cpSR.sortingLayerName = SortingLayerName;
            cpSR.sortingOrder = BaseSortingOrder + 1;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }

        // 7. 逻辑初始化
        ActivateCombatBrainsSafe(data, comps, isEnemy);

        // 8. 阴影处理 (确保在逻辑加载后)
        UnitFactionShadow shadowComp = GetComponent<UnitFactionShadow>();
        if (shadowComp == null) shadowComp = gameObject.AddComponent<UnitFactionShadow>();

        shadowComp.EnsureShadowObject();
        Transform shadowTrans = shadowComp.GetShadowTransform();
        if (shadowTrans != null)
        {
            shadowTrans.SetParent(chassisObj.transform, false);
            shadowTrans.SetAsFirstSibling();
        }
        ApplyFinalShadowSettings(isEnemy, enemySO, comps, chassisSR, chassisObj.transform);

        // 9. 动画初始化
        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim == null) procAnim = gameObject.AddComponent<ProceduralAnimator2D>();
        procAnim.SetTargetVisual(chassisObj.transform);
        procAnim.RefreshBaseState();
    }

    private void ActivateCombatBrainsSafe(SavedUnitProfile data, InstancedComponent[] comps, bool isEnemy)
    {
        cachedCombatData = new RuntimeChimeraData();
        cachedCombatData.UnitID = data.UnitID;
        cachedCombatData.Assemble(data.ChassisData, comps);

        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver == null) receiver = gameObject.AddComponent<DamageReceiver>();

        receiver.isEnemy = isEnemy;
        receiver.Initialize(cachedCombatData.MaxHP, cachedCombatData.MaxAP);

        if (!isEnemy && data.CurrentHP > 0) receiver.CurrentHP = Mathf.Min(data.CurrentHP, cachedCombatData.MaxHP);
        else receiver.CurrentHP = cachedCombatData.MaxHP;

        // 自动补齐控制器
        ChimeraAIController ai = GetComponent<ChimeraAIController>();
        if (ai == null) ai = gameObject.AddComponent<ChimeraAIController>();
        ai.Initialize(cachedCombatData);

        MechSkillController skillCtrl = GetComponent<MechSkillController>();
        if (skillCtrl == null) skillCtrl = gameObject.AddComponent<MechSkillController>();
        skillCtrl.Initialize(cachedCombatData);

        int weaponIndex = 0;
        Transform chassisBase = VisualRoot.Find("Visual_ChassisBase");
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] != null && comps[i].BaseData.Type == ComponentType.Weapon)
            {
                var slotDef = data.ChassisData.Sockets[i];
                Transform socketTrans = chassisBase.FindRecursive($"Socket_{slotDef.SlotName}");
                if (socketTrans != null)
                {
                    WeaponModule weaponScript = socketTrans.gameObject.GetComponent<WeaponModule>();
                    if (weaponScript == null) weaponScript = socketTrans.gameObject.AddComponent<WeaponModule>();
                    weaponScript.Initialize(cachedCombatData.EquippedWeapons[weaponIndex], cachedCombatData, cachedCombatData.LogicCenterOffset, this.transform);
                }
                weaponIndex++;
            }
        }
    }

    private void ApplyFinalShadowSettings(bool isEnemy, EnemyDataSO enemySO, InstancedComponent[] comps, SpriteRenderer chassisSR, Transform chassisTrans)
    {
        UnitFactionShadow shadowComp = GetComponent<UnitFactionShadow>();
        if (shadowComp == null) return;

        bool hasComponentOverride = comps.Any(c => c != null && c.BaseData.Type == ComponentType.Movement && c.BaseData.OverrideShadow);
        ComponentDataSO moveCompSO = comps.FirstOrDefault(c => c != null && c.BaseData.Type == ComponentType.Movement && c.BaseData.OverrideShadow)?.BaseData;

        if (isEnemy && enemySO != null && enemySO.OverrideShadow)
            shadowComp.SetupManualShadow(true, enemySO.ShadowWidth, enemySO.ShadowHeight, enemySO.ShadowOffset);
        else if (hasComponentOverride)
            shadowComp.SetupManualShadow(isEnemy, moveCompSO.ShadowWidth, moveCompSO.ShadowHeight, moveCompSO.ShadowOffset);
        else
        {
            float finalLowestY = -(chassisSR.sprite.bounds.size.y / 2f);
            float finalMaxWidth = chassisSR.bounds.size.x;
            bool foundMovement = false;
            foreach (Transform socket in chassisTrans)
            {
                if (socket.name.Contains("_MovementType"))
                {
                    SpriteRenderer moveSR = socket.GetComponentInChildren<SpriteRenderer>();
                    if (moveSR != null && moveSR.sprite != null)
                    {
                        float currentBottomY = socket.localPosition.y - (moveSR.sprite.bounds.size.y * moveSR.transform.lossyScale.y / transform.lossyScale.y / 2f);
                        if (!foundMovement) { finalLowestY = currentBottomY; finalMaxWidth = moveSR.bounds.size.x; foundMovement = true; }
                        else { finalLowestY = Mathf.Min(finalLowestY, currentBottomY); finalMaxWidth = Mathf.Max(finalMaxWidth, moveSR.bounds.size.x); }
                    }
                }
            }
            shadowComp.SetupModularShadow(isEnemy, finalMaxWidth, finalLowestY);
        }
    }

    public void ExecuteBattleStartProtocol()
    {
        if (this.cachedCombatData == null) return;
        DamageReceiver dr = GetComponent<DamageReceiver>();
        bool isEnemyMech = dr != null && dr.isEnemy;

        ECAContext startContext = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = this.transform,
            SourceEntity = this.transform,
            ChassisData = this.cachedCombatData,
            IsEnemyFire = isEnemyMech
        };
        foreach (var action in this.cachedCombatData.GlobalOnBattleStartActions) if (action != null) action.Execute(startContext);
    }

    private void OnMouseDown() { if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase) return; isDragging = true; dragStartPos = transform.position; TintMech(new Color(1f, 1f, 1f, 0.5f)); if (rb != null) rb.isKinematic = true; if (physicsCol != null) physicsCol.enabled = false; }
    private void OnMouseDrag() { if (!isDragging) return; Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition); transform.position = new Vector3(mousePos.x, mousePos.y, -0.01f); }
    private void OnMouseUp() { if (!isDragging) return; isDragging = false; TintMech(Color.white); if (rb != null) rb.isKinematic = false; if (EventSystem.current.IsPointerOverGameObject()) { if (physicsCol != null) physicsCol.enabled = true; RecycleToHangar(); return; } int noDeployLayerMask = LayerMask.GetMask("NoDeploy"); Collider2D forbiddenHit = Physics2D.OverlapCircle(transform.position, 0.5f, noDeployLayerMask); bool isValidZone = Physics2D.OverlapPointAll(transform.position).Any(h => h.CompareTag("DeployZone")); if (forbiddenHit != null || !isValidZone) transform.position = dragStartPos; if (physicsCol != null) physicsCol.enabled = true; }
    private void RecycleToHangar() { if (bindedData != null) bindedData.IsDeployed = false; if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.RefreshHangar(); Destroy(gameObject); }
    private void TintMech(Color targetColor)
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers)
        {
            // 【关键保护】：如果这个贴图是影子，直接跳过，不准改它的颜色！
            if (sr.gameObject.name == "Logic_Visual_Shadow") continue;

            sr.color = targetColor;
        }
    }
    public void SyncPostCombatState() { if (bindedData == null) return; DamageReceiver receiver = GetComponent<DamageReceiver>(); if (receiver != null) { bindedData.CurrentHP = Mathf.Max(0, receiver.CurrentHP); bindedData.CurrentAP = receiver.MaxAP; } }
    private void LateUpdate() { EnforceArenaBounds(); }
    private void EnforceArenaBounds() { if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentArenaSize.x == 0) return; Vector2 center = CombatDirector.Instance.CurrentArenaCenter; Vector2 size = CombatDirector.Instance.CurrentArenaSize; float minX = center.x - size.x / 2f, maxX = center.x + size.x / 2f, minY = center.y - size.y / 2f, maxY = center.y + size.y / 2f; Vector3 cp = transform.position; float cx = Mathf.Clamp(cp.x, minX, maxX), cy = Mathf.Clamp(cp.y, minY, maxY); if (cp.x != cx || cp.y != cy) { transform.position = new Vector3(cx, cy, cp.z); if (rb != null) rb.velocity = Vector2.zero; } }
}

public static class TransformExtensions
{
    public static Transform FindRecursive(this Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}