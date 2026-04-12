// --- START OF FILE MechUnit2D.cs ---
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

public class MechUnit2D : MonoBehaviour
{
    private SavedUnitProfile bindedData;

    [Header("=== 核心引用 ===")]
    public Transform VisualRoot;

    [Header("=== 2D 排序层控制 ===")]
    public string SortingLayerName = "Entities";
    public int BaseSortingOrder = 0;

    [Header("=== 战场视觉与物理缩放 ===")]
    [Range(0.1f, 5f)]
    public float GlobalBattleScale = 1.0f;

    private Rigidbody2D rb;
    private Collider2D physicsCol;
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

        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = SortingLayerName;
    }

    public void InitUnitData(SavedUnitProfile data)
    {
        this.bindedData = data;
        this.name = $"[UNIT] {data.UnitName}";

        foreach (Transform child in VisualRoot) Destroy(child.gameObject);

        transform.position = new Vector3(transform.position.x, transform.position.y, -0.01f);
        transform.localScale = Vector3.one * GlobalBattleScale;

        gameObject.layer = LayerMask.NameToLayer("Player_Body");

        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
        ((BoxCollider2D)physicsCol).isTrigger = false;

        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        chassisObj.layer = LayerMask.NameToLayer("Player_Hitbox");

        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        Vector2 spriteSize = chassisSR.sprite.bounds.size;
        ((BoxCollider2D)physicsCol).size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        ((BoxCollider2D)physicsCol).offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.bounds.extents.y));

        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.9f);
        hitboxCol.offset = Vector2.zero;

        DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
        if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
        sorter.YOffset = -(spriteSize.y / 2f);

        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;
            var slotDef = data.ChassisData.Sockets[slotIdx];

            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.layer = this.gameObject.layer;
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.layer = this.gameObject.layer;
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.layer = this.gameObject.layer;
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer compSR = visObj.AddComponent<SpriteRenderer>();
            compSR.sprite = comp.BaseData.ComponentIcon;
            compSR.sortingLayerName = SortingLayerName;
            compSR.sortingOrder = BaseSortingOrder + 1;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }

        ActivateCombatBrains(data);

        BuffManager buffMgr = GetComponent<BuffManager>();
        if (buffMgr == null) buffMgr = gameObject.AddComponent<BuffManager>();

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>();
        if (procAnim == null) procAnim = gameObject.AddComponent<ProceduralAnimator2D>();
        procAnim.SetTargetVisual(chassisObj.transform);
        procAnim.RefreshBaseState();
    }

    private void ActivateCombatBrains(SavedUnitProfile data)
    {
        RuntimeChimeraData combatData = new RuntimeChimeraData();
        combatData.UnitID = data.UnitID;

        InstancedComponent[] tempInstances = new InstancedComponent[data.ChassisData.Sockets.Count];
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);

            if (compInstance != null)
            {
                if (compInstance.CurrentLevel <= 0) compInstance.CurrentLevel = 1;
                tempInstances[slotIdx] = compInstance;
            }
        }

        // 调用黑盒装配，读入全部数据！
        combatData.Assemble(data.ChassisData, tempInstances);

        DamageReceiver receiver = GetComponent<DamageReceiver>() ?? gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = false;

        // 防猝死安全锁
        float safeMaxHP = combatData.MaxHP > 0 ? combatData.MaxHP : 100f;
        float safeMaxAP = combatData.MaxAP > 0 ? combatData.MaxAP : 0f;
        receiver.Initialize(safeMaxHP, safeMaxAP);

        // 同步受损血量
        if (data.CurrentHP > 0) receiver.CurrentHP = Mathf.Min(data.CurrentHP, safeMaxHP);
        else receiver.CurrentHP = safeMaxHP;

        ChimeraAIController aiController = GetComponent<ChimeraAIController>() ?? gameObject.AddComponent<ChimeraAIController>();
        aiController.Initialize(combatData);

        // 挂载主动技能大脑
        MechSkillController skillCtrl = GetComponent<MechSkillController>() ?? gameObject.AddComponent<MechSkillController>();
        skillCtrl.Initialize(combatData);

        int weaponDataIndex = 0;
        for (int i = 0; i < tempInstances.Length; i++)
        {
            var compInstance = tempInstances[i];
            if (compInstance != null && compInstance.BaseData.Type == ComponentType.Weapon)
            {
                if (weaponDataIndex >= combatData.EquippedWeapons.Count) break;

                var slotDef = data.ChassisData.Sockets[i];
                Transform socketTrans = VisualRoot.FindRecursive($"Socket_{slotDef.SlotName}");

                if (socketTrans != null)
                {
                    WeaponModule weaponScript = socketTrans.gameObject.AddComponent<WeaponModule>();
                    weaponScript.Initialize(combatData.EquippedWeapons[weaponDataIndex], combatData.LogicCenterOffset, this.transform);
                }
                weaponDataIndex++;
            }
        }
    }

    private void OnMouseDown()
    {
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase) return;

        isDragging = true;
        dragStartPos = transform.position;
        TintMech(new Color(1f, 1f, 1f, 0.5f));
        if (rb != null) rb.isKinematic = true;
        if (physicsCol != null) physicsCol.enabled = false;
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mousePos.x, mousePos.y, -0.01f);
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        TintMech(Color.white);
        if (rb != null) rb.isKinematic = false;

        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (physicsCol != null) physicsCol.enabled = true;
            RecycleToHangar();
            return;
        }

        int noDeployLayerMask = LayerMask.GetMask("NoDeploy");
        Collider2D forbiddenHit = Physics2D.OverlapCircle(transform.position, 0.5f, noDeployLayerMask);

        if (forbiddenHit != null)
        {
            transform.position = dragStartPos;
            if (physicsCol != null) physicsCol.enabled = true;
            return;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
        bool isValidZone = false;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("DeployZone"))
            {
                isValidZone = true;
                break;
            }
        }

        if (!isValidZone) transform.position = dragStartPos;
        if (physicsCol != null) physicsCol.enabled = true;
    }

    private void RecycleToHangar()
    {
        if (bindedData != null) bindedData.IsDeployed = false;
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.RefreshHangar();
        Destroy(gameObject);
    }

    private void TintMech(Color targetColor)
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers) sr.color = targetColor;
    }

    public void SyncPostCombatState()
    {
        if (bindedData == null) return;
        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            bindedData.CurrentHP = Mathf.Max(0, receiver.CurrentHP);
            bindedData.CurrentAP = receiver.MaxAP;
        }
    }

    private void LateUpdate()
    {
        EnforceArenaBounds();
    }

    private void EnforceArenaBounds()
    {
        if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentArenaSize.x == 0) return;

        Vector2 center = CombatDirector.Instance.CurrentArenaCenter;
        Vector2 size = CombatDirector.Instance.CurrentArenaSize;

        float minX = center.x - size.x / 2f;
        float maxX = center.x + size.x / 2f;
        float minY = center.y - size.y / 2f;
        float maxY = center.y + size.y / 2f;

        Vector3 currentPos = transform.position;
        float clampedX = Mathf.Clamp(currentPos.x, minX, maxX);
        float clampedY = Mathf.Clamp(currentPos.y, minY, maxY);

        if (currentPos.x != clampedX || currentPos.y != clampedY)
        {
            transform.position = new Vector3(clampedX, clampedY, currentPos.z);
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
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