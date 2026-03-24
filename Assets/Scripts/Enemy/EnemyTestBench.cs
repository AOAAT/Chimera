using UnityEngine;

[ExecuteInEditMode]
public class EnemyTestBench : MonoBehaviour
{
    [Header("=== 拖入怪物图纸进行调试 ===")]
    public EnemyDataSO TargetEnemyData;

    [Header("=== 环境缩放比例 ===")]
    public float GlobalVisualScale = 1.0f;

    private SpriteRenderer myRenderer;
    private GameObject visualHitboxNode;

    private void Update()
    {
        if (!Application.isPlaying) UpdateVisualPreview();
    }

    private void UpdateVisualPreview()
    {
        if (TargetEnemyData == null) return;

        if (myRenderer == null)
        {
            myRenderer = GetComponentInChildren<SpriteRenderer>();
            if (myRenderer == null)
            {
                visualHitboxNode = new GameObject("Enemy_Visual_Hitbox");
                visualHitboxNode.transform.SetParent(this.transform, false);
                myRenderer = visualHitboxNode.AddComponent<SpriteRenderer>();
            }
        }

        transform.localScale = Vector3.one * GlobalVisualScale;
        if (myRenderer.sprite != TargetEnemyData.EnemySprite)
            myRenderer.sprite = TargetEnemyData.EnemySprite;
    }

    private void Start()
    {
        if (Application.isPlaying && TargetEnemyData != null) AwakenFlesh();
    }

    private void AwakenFlesh()
    {
        gameObject.name = "[Awakened] " + TargetEnemyData.EnemyName;

        // 1. 注入真实刚体 (读取字典里的绝对质量 Mass，不再是 AddedMass)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = 3f;
        rb.mass = Mathf.Max(TargetEnemyData.GetStat(StatType.Mass), 1f);

        // 2. 注入脚底物理推挤框 (防穿模)
        BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
        physicsCol.isTrigger = false;
        physicsCol.size = new Vector2(0.5f, 0.5f);

        // 3. 处理视觉子节点与接弹受击框
        if (visualHitboxNode == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visualHitboxNode = sr.gameObject;
        }
        if (visualHitboxNode != null)
        {
            BoxCollider2D hitBox = visualHitboxNode.GetComponent<BoxCollider2D>();
            if (hitBox == null) hitBox = visualHitboxNode.AddComponent<BoxCollider2D>();
            hitBox.isTrigger = true;
            hitBox.size = new Vector2(1f, 1f);
        }

        // 4. 注入受击躯壳 (DamageReceiver)
        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver == null) receiver = gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = true;

        // 读取绝对生命值和护甲 (HP, AP)
        float maxHp = TargetEnemyData.GetStat(StatType.HP);
        receiver.Initialize(maxHp > 0 ? maxHp : 1f, TargetEnemyData.GetStat(StatType.AP), myRenderer);

        // 5. 注入灵魂大脑
        EnemyBrain brain = GetComponent<EnemyBrain>();
        if (brain == null) brain = gameObject.AddComponent<EnemyBrain>();
        brain.MyData = TargetEnemyData;
    }
}