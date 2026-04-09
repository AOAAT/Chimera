#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EncounterLayoutSO))]
public class EncounterLayoutEditor : Editor
{
    private EncounterLayoutSO layout;
    private int selectedEnemyIndex = -1;
    private int selectedZoneIndex = -1;
    private bool isDraggingEnemy = false;
    private bool isDraggingZone = false;

    private void OnEnable()
    {
        layout = (EncounterLayoutSO)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("🗺️ 战术沙盘 2.0 (所见即所得)", EditorStyles.boldLabel);

        // === 1. 数据侦察：扒取预制体的真实参数 ===
        Vector2 arenaWorldSize = new Vector2(20f, 20f); // 兜底大小
        Texture2D arenaTexture = null;

        if (layout.ArenaReference != null)
        {
            // 精准读取物理碰撞箱的绝对大小 (考虑到 Scale 缩放)
            BoxCollider2D col = layout.ArenaReference.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                arenaWorldSize = new Vector2(
                    col.size.x * layout.ArenaReference.transform.localScale.x,
                    col.size.y * layout.ArenaReference.transform.localScale.y
                );
            }

            // 读取场地背景贴图
            SpriteRenderer sr = layout.ArenaReference.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                arenaTexture = sr.sprite.texture;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("👆 请在上方 Arena Reference 拖入场地预制体，以解锁真实贴图和物理边界！", MessageType.Warning);
        }

        // === 2. 屏幕排版：计算等比例自适应画布 ===
        float maxWidth = EditorGUIUtility.currentViewWidth - 40f;
        float aspect = arenaWorldSize.x / (arenaWorldSize.y == 0 ? 1 : arenaWorldSize.y);
        float drawWidth = Mathf.Min(maxWidth, 450f); // 限制最大宽度
        float drawHeight = drawWidth / aspect;

        Rect arenaRect = GUILayoutUtility.GetRect(drawWidth, drawHeight, GUILayout.Width(drawWidth), GUILayout.Height(drawHeight));

        // === 3. 铺设底板 ===
        if (arenaTexture != null)
        {
            // 画出真实的场景贴图！
            GUI.DrawTexture(arenaRect, arenaTexture, ScaleMode.StretchToFill);
        }
        else
        {
            // 没图就画高贵的深空灰
            EditorGUI.DrawRect(arenaRect, new Color(0.1f, 0.1f, 0.15f));
        }

        // 画出物理中心十字线 (0,0 点)
        Vector2 centerGui = WorldToGUIPos(Vector2.zero, arenaRect, arenaWorldSize);
        EditorGUI.DrawRect(new Rect(arenaRect.x, centerGui.y, arenaRect.width, 1), new Color(1, 1, 1, 0.3f));
        EditorGUI.DrawRect(new Rect(centerGui.x, arenaRect.y, 1, arenaRect.height), new Color(1, 1, 1, 0.3f));

        // === 4. 交互核心：鼠标事件拦截 ===
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;

        if (arenaRect.Contains(mousePos))
        {
            // 鼠标按下：判定选了谁？
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                selectedEnemyIndex = -1;
                selectedZoneIndex = -1;

                // 优先检测：是否点中了某个敌人小绿点？
                for (int i = 0; i < layout.Enemies.Count; i++)
                {
                    Vector2 guiPos = WorldToGUIPos(layout.Enemies[i].LocalPosition, arenaRect, arenaWorldSize);
                    if (Vector2.Distance(mousePos, guiPos) < 12f)
                    {
                        selectedEnemyIndex = i;
                        isDraggingEnemy = true;
                        e.Use();
                        break;
                    }
                }

                // 如果没点中敌人，检测：是否点中了某个红框禁区？
                if (!isDraggingEnemy)
                {
                    for (int i = 0; i < layout.ForbiddenZones.Count; i++)
                    {
                        Rect zoneRect = layout.ForbiddenZones[i];
                        Vector2 guiPos = WorldToGUIPos(new Vector2(zoneRect.x, zoneRect.y), arenaRect, arenaWorldSize);
                        Vector2 guiSize = new Vector2((zoneRect.width / arenaWorldSize.x) * arenaRect.width, (zoneRect.height / arenaWorldSize.y) * arenaRect.height);
                        Rect zoneGuiRect = new Rect(guiPos.x, guiPos.y, guiSize.x, guiSize.y);

                        if (zoneGuiRect.Contains(mousePos))
                        {
                            selectedZoneIndex = i;
                            isDraggingZone = true;
                            e.Use();
                            break;
                        }
                    }
                }
            }

            // 鼠标拖拽：改写绝对坐标！
            if (e.type == EventType.MouseDrag)
            {
                if (isDraggingEnemy && selectedEnemyIndex != -1)
                {
                    Undo.RecordObject(layout, "Move Enemy");
                    layout.Enemies[selectedEnemyIndex].LocalPosition = GUIToWorldPos(mousePos, arenaRect, arenaWorldSize);
                    EditorUtility.SetDirty(layout);
                    e.Use();
                }
                else if (isDraggingZone && selectedZoneIndex != -1)
                {
                    Undo.RecordObject(layout, "Move Zone");
                    Rect currentRect = layout.ForbiddenZones[selectedZoneIndex];
                    Vector2 newWorldPos = GUIToWorldPos(mousePos, arenaRect, arenaWorldSize);
                    // 保持宽高不变，只移动左上角原点
                    layout.ForbiddenZones[selectedZoneIndex] = new Rect(newWorldPos.x, newWorldPos.y, currentRect.width, currentRect.height);
                    EditorUtility.SetDirty(layout);
                    e.Use();
                }
            }

            // 鼠标抬起：释放灵魂
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                isDraggingEnemy = false;
                isDraggingZone = false;
            }
        }

        // === 5. 渲染图层：画禁区与敌人 ===

        // 渲染禁飞区 (红框)
        for (int i = 0; i < layout.ForbiddenZones.Count; i++)
        {
            Rect zone = layout.ForbiddenZones[i];
            Vector2 guiPos = WorldToGUIPos(new Vector2(zone.x, zone.y), arenaRect, arenaWorldSize);
            Vector2 guiSize = new Vector2((zone.width / arenaWorldSize.x) * arenaRect.width, (zone.height / arenaWorldSize.y) * arenaRect.height);
            Rect drawRect = new Rect(guiPos.x, guiPos.y, guiSize.x, guiSize.y);

            // 被选中的禁区颜色更深一点
            Color zoneColor = (i == selectedZoneIndex) ? new Color(1f, 0f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.3f);
            EditorGUI.DrawRect(drawRect, zoneColor);

            // 选中的禁区画一个黄色边框方便识别
            if (i == selectedZoneIndex)
            {
                GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = Texture2D.whiteTexture; // 简易边框技巧
                GUI.backgroundColor = new Color(1, 1, 0, 0.5f);
                GUI.Box(drawRect, "", boxStyle);
                GUI.backgroundColor = Color.white;
            }
        }

        // 渲染敌人 (绿点)
        for (int i = 0; i < layout.Enemies.Count; i++)
        {
            Vector2 guiPos = WorldToGUIPos(layout.Enemies[i].LocalPosition, arenaRect, arenaWorldSize);
            Rect dotRect = new Rect(guiPos.x - 6, guiPos.y - 6, 12, 12);

            Color dotColor = (i == selectedEnemyIndex) ? Color.yellow : Color.green;
            EditorGUI.DrawRect(dotRect, dotColor);

            string nameLabel = layout.Enemies[i].EnemyType != null ? layout.Enemies[i].EnemyType.EnemyName : "空槽位";
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(guiPos.x + 10, guiPos.y - 8, 100, 20), nameLabel, labelStyle);
        }

        if (GUI.changed || isDraggingEnemy || isDraggingZone) Repaint();
    }

    // --- 极其精准的坐标转换矩阵 ---
    private Vector2 WorldToGUIPos(Vector2 worldPos, Rect guiRect, Vector2 arenaSize)
    {
        float x = guiRect.x + (worldPos.x / arenaSize.x + 0.5f) * guiRect.width;
        float y = guiRect.y + (0.5f - worldPos.y / arenaSize.y) * guiRect.height;
        return new Vector2(x, y);
    }

    private Vector2 GUIToWorldPos(Vector2 guiPos, Rect guiRect, Vector2 arenaSize)
    {
        float x = ((guiPos.x - guiRect.x) / guiRect.width - 0.5f) * arenaSize.x;
        float y = (0.5f - (guiPos.y - guiRect.y) / guiRect.height) * arenaSize.y;

        return new Vector2(Mathf.Round(x * 2) / 2f, Mathf.Round(y * 2) / 2f); // 0.5的网格吸附
    }
}
#endif