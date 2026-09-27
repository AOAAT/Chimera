// --- START OF FILE ArenaBackgroundConfig.cs ---
using System.Collections.Generic;
using UnityEngine;

public class ArenaBackgroundConfig : MonoBehaviour
{
    [Header("=== 随机战场背景池 ===")]
    [Tooltip("每次进入此地形时，将从以下贴图中随机抽取一张作为背景。\n如果列表为空，则向下兼容，使用 SpriteRenderer 上的默认贴图。")]
    public List<Sprite> RandomBackgrounds = new List<Sprite>();
}