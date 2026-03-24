using UnityEngine;

[ExecuteInEditMode]
public class EnemyTestBench : MonoBehaviour
{
    [Header("=== 拖入怪物图纸进行调试 ===")]
    public EnemyDataSO TargetEnemyData;

    [Header("=== 环境缩放比例 (仅用于测试台预览) ===")]
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

        // 1. 优先尝试寻找已有的贴图组件
        if (myRenderer == null)
        {
            myRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // 2. 如果彻底找不到，再无中生有造一个
        if (myRenderer == null)
        {
            visualHitboxNode = new GameObject("Enemy_Visual_Hitbox");
            visualHitboxNode.transform.SetParent(this.transform, false);
            myRenderer = visualHitboxNode.AddComponent<SpriteRenderer>();
        }
        else
        {
            // 👇【核心修复】：只要 renderer 存在，强行把它的宿主节点同步给 visualHitboxNode！
            visualHitboxNode = myRenderer.gameObject;
        }

        // 3. 应用图纸专属缩放比例
        if (visualHitboxNode != null)
        {
            visualHitboxNode.transform.localScale = Vector3.one * TargetEnemyData.VisualScaleMultiplier;
        }

        // 4. 测试台根节点自身缩放 (方便场景拉远看)
        transform.localScale = Vector3.one * GlobalVisualScale;

        // 5. 替换贴图
        if (myRenderer.sprite != TargetEnemyData.EnemySprite)
        {
            myRenderer.sprite = TargetEnemyData.EnemySprite;
        }
    }
    private void Start()
    {
        if (Application.isPlaying && TargetEnemyData != null) AwakenFlesh();
    }

    private void AwakenFlesh()
    {
        gameObject.name = "[Awakened] " + TargetEnemyData.EnemyName;

        // 1. 注入物理推挤归属层
        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");

        // 2. 注入真实刚体
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = 3f;
        rb.mass = Mathf.Max(TargetEnemyData.GetStat(StatType.Mass), 1f);

        // 3. 处理视觉子节点与接弹受击框
        if (visualHitboxNode == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visualHitboxNode = sr.gameObject;
        }

        if (visualHitboxNode != null)
        {
            visualHitboxNode.layer = LayerMask.NameToLayer("Enemy_Hitbox");

            // 👇【核心修复 2】：实战觉醒时，严格执行图纸的缩放率！
            visualHitboxNode.transform.localScale = Vector3.one * TargetEnemyData.VisualScaleMultiplier;

            // 自动包裹图片的触发器（接子弹用）
            BoxCollider2D hitBox = visualHitboxNode.GetComponent<BoxCollider2D>();
            if (hitBox == null) hitBox = visualHitboxNode.AddComponent<BoxCollider2D>();
            hitBox.isTrigger = true;
            // 不再写死 size，Unity 会自动根据 SpriteRenderer 撑满它！

            // 👇【核心修复 3】：动态裁切物理推挤脚底板！
            SpriteRenderer visSr = visualHitboxNode.GetComponent<SpriteRenderer>();
            if (visSr != null && visSr.sprite != null)
            {
                // 获取缩放后的真实世界尺寸
                Vector2 realSize = visSr.sprite.bounds.size * TargetEnemyData.VisualScaleMultiplier;

                BoxCollider2D physicsCol = GetComponent<BoxCollider2D>();
                if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
                physicsCol.isTrigger = false;


                // 削肉剔骨：压缩到真实缩放后脚底的 30%，完美贴合！
                physicsCol.size = new Vector2(realSize.x * 0.8f, realSize.y * 0.3f);

                // 坐标偏移公式：向下移动半个身位，再拉回半个碰撞体的高度，死死咬住脚底板！
                physicsCol.offset = new Vector2(0f, -(realSize.y / 2f) + (physicsCol.size.y / 2f));

                // 深度排序引擎也必须使用真实的半身位
                DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
                if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
                sorter.YOffset = -(realSize.y / 2f);
            }
        }

        // 4. 注入受击躯壳 (DamageReceiver)
        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver == null) receiver = gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = true;

        float maxHp = TargetEnemyData.GetStat(StatType.HP);
        receiver.Initialize(maxHp > 0 ? maxHp : 1f, TargetEnemyData.GetStat(StatType.AP), myRenderer);

        // 5. 注入灵魂大脑
        EnemyBrain brain = GetComponent<EnemyBrain>();
        if (brain == null) brain = gameObject.AddComponent<EnemyBrain>();
        brain.MyData = TargetEnemyData;
    }
}