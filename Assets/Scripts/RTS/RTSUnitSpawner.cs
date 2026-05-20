// --- START OF FILE RTSUnitSpawner.cs ---
using UnityEngine;

public class RTSUnitSpawner : MonoBehaviour
{
    public GameObject MechBasePrefab;

    private void Update()
    {
        // 按 S 降落机甲
        if (Input.GetKeyDown(KeyCode.S))
        {
            PerformDeployment();
        }
    }

    private void PerformDeployment()
    {
        var hangar = PlayerInventoryManager.Instance.HangarUnits;
        SavedUnitProfile profile = null;
        foreach (var p in hangar) { if (p != null) { profile = p; break; } }

        if (profile == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 snapped = RTSGridSystem.Instance.GetSnappedWorldPos(mouseWorld);
        snapped.z = 0;

        GameObject go = Instantiate(MechBasePrefab, snapped, Quaternion.identity);
        MechUnit2D unit = go.GetComponent<MechUnit2D>();
        unit.InitUnitData(profile);

        // --- ⚔️ RTS 物理规格强制对齐 ---
        SetupRTSPhysics(go);
    }

    private void SetupRTSPhysics(GameObject go)
    {
        // 1. 彻底禁用老式的脚底 BoxCollider
        var oldCol = go.GetComponent<BoxCollider2D>();
        if (oldCol != null) oldCol.enabled = false;

        // 2. 注入圆形碰撞核 (直径 0.7m，小于格子的 1.0m)
        var circle = go.GetComponent<CircleCollider2D>();
        if (circle == null) circle = go.AddComponent<CircleCollider2D>();

        circle.radius = 0.35f; // 半径 0.35 = 直径 0.7
        circle.isTrigger = false;

        // 3. 刚体属性对齐
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.drag = 5f;
            rb.angularDrag = 10f; // 减少旋转抖动
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }
}