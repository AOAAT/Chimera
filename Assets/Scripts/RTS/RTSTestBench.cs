using System.Collections.Generic;
using UnityEngine;

public class RTSTestBench : MonoBehaviour
{

    [Header("=== 敌军配置 (按 E 键) ===")]
    [Tooltip("在 Project 窗口找到你想测试的敌人图纸拖进来")]
    public List<EnemyDataSO> EnemyPool = new List<EnemyDataSO>();
    public GameObject BaseEnemyPrefab; // 挂载 EnemyBrain 的那个基础预制体

    private void Update()
    {

        // --- E 键：生成敌对单位 ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            SpawnEnemyAtMouse();
        }
    }


    private void SpawnEnemyAtMouse()
    {
        if (EnemyPool.Count == 0 || BaseEnemyPrefab == null)
        {
            Debug.LogWarning("【测试台】请先在 Inspector 里配置敌军图纸和预制体！");
            return;
        }

        Vector3 pos = GetMouseWorldPos();
        EnemyDataSO randomData = EnemyPool[Random.Range(0, EnemyPool.Count)];

        GameObject go = Instantiate(BaseEnemyPrefab, pos, Quaternion.identity);

        // 1. 初始化大脑
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        if (brain != null)
        {
            brain.MyData = randomData;
            // 唤醒大脑 (模拟 Start 逻辑)
            brain.enabled = true;
        }

        // 2. 物理重塑
        SetupRTSPhysics(go, "Enemy_Body");

        // 3. 户口登记 (登记后，玩家的自动索敌和 ECA 溅射才会生效)
        DamageReceiver dr = go.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            dr.isEnemy = true;
            if (!CombatDirector.ActiveEnemies.Contains(dr))
                CombatDirector.ActiveEnemies.Add(dr);
        }
        go.GetComponent<MechUnit2D>()?.ExecuteBattleStartProtocol();

        Debug.Log($"<color=red>【测试】敌军 {randomData.EnemyName} 已通电并进入临战状态。</color>");
    }

    private void SetupRTSPhysics(GameObject go, string layerName)
    {
        go.layer = LayerMask.NameToLayer(layerName);

        // 禁用旧 Box
        BoxCollider2D oldBox = go.GetComponent<BoxCollider2D>();
        if (oldBox != null) oldBox.enabled = false;

        // 强换圆形
        CircleCollider2D circle = go.GetComponent<CircleCollider2D>();
        if (circle == null) circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.35f;
        circle.isTrigger = false;

        // 刚体加固
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        return mousePos;
    }
}