// --- RTSMapVisuals.cs (拼接修正版) ---
using UnityEngine;

public class RTSMapVisuals : MonoBehaviour
{
    public Sprite BaseTileSprite;
    public Color ColorTech = new Color(0.1f, 0.4f, 0.7f, 1f);
    public Color ColorWasteland = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color ColorFlesh = new Color(0.6f, 0.1f, 0.2f, 1f);

    private void Start()
    {
        Invoke(nameof(DrawTiles), 0.1f);
    }

    private void DrawTiles()
    {
        var sys = RTSGridSystem.Instance;
        GameObject root = new GameObject("Visual_Grid_Tiles");
        root.transform.SetParent(this.transform);

        for (int x = 0; x < sys.MapWidth; x++)
        {
            float progress = (float)x / sys.MapWidth;
            Color currentTileColor = (progress < 0.5f)
                ? Color.Lerp(ColorTech, ColorWasteland, progress * 2f)
                : Color.Lerp(ColorWasteland, ColorFlesh, (progress - 0.5f) * 2f);

            for (int y = 0; y < sys.MapHeight; y++)
            {
                GridCell cell = sys.GetCell(x, y);
                if (cell == null) continue;

                GameObject tileObj = new GameObject($"Tile_{x}_{y}");
                tileObj.transform.SetParent(root.transform);
                // 放在 Z=1 确保机甲(Z=0)在它前面
                tileObj.transform.position = new Vector3(cell.WorldPos.x, cell.WorldPos.y, 1f);

                SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
                sr.sprite = BaseTileSprite;
                sr.color = currentTileColor;
                sr.sortingLayerName = "Floor";

                if (cell.ScrapDensity > 0)
                {
                    GameObject resMark = new GameObject("Res_Icon");
                    resMark.transform.SetParent(tileObj.transform);
                    resMark.transform.localPosition = Vector3.zero;
                    SpriteRenderer rsr = resMark.AddComponent<SpriteRenderer>();
                    rsr.sprite = BaseTileSprite;
                    rsr.color = new Color(1, 0.9f, 0, 0.6f); // 半透明金黄
                    rsr.transform.localScale = Vector3.one * 0.3f; // 资源点只是个装饰，缩到0.3
                    rsr.sortingOrder = 1;
                }
            }
        }
    }
}