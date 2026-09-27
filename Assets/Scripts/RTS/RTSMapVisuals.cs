using UnityEngine;

// Serialized chunks are visible in edit mode and reused in play mode.
public class RTSMapVisuals : MonoBehaviour
{
    public Sprite BaseTileSprite;
    public Color ColorTech = new Color(.1f,.4f,.7f,1);
    public Color ColorWasteland = new Color(.3f,.3f,.3f,1);
    public Color ColorFlesh = new Color(.6f,.1f,.2f,1);
    public const int ChunkSize = 32;
    public const string GeneratedRootName = "Visual_Grid_Tiles";
    [SerializeField, HideInInspector] private int previewWidth, previewHeight;
    [SerializeField, HideInInspector] private float previewCellSize;
    [SerializeField, HideInInspector] private Vector2 previewOrigin;
    [SerializeField, HideInInspector] private Sprite previewSprite;
    [SerializeField, HideInInspector] private Color previewColor;
    public Transform GeneratedRoot => transform.Find(GeneratedRootName);

    public bool PreviewMatches(RTSGridSystem grid) => grid != null && GeneratedRoot != null &&
        previewWidth == grid.MapWidth && previewHeight == grid.MapHeight && previewCellSize == grid.CellSize &&
        previewOrigin == grid.GridOrigin && previewSprite == BaseTileSprite && previewColor == ColorFor(grid.DefaultFlavor) &&
        GeneratedRoot.position.sqrMagnitude < .000001f && Quaternion.Angle(GeneratedRoot.rotation,Quaternion.identity)<.01f &&
        (GeneratedRoot.lossyScale-Vector3.one).sqrMagnitude<.000001f;

    private void Start()
    {
        var grid = GetComponent<RTSGridSystem>() ?? RTSGridSystem.Instance;
        if (grid == null) return;
        if (!PreviewMatches(grid)) Rebuild(grid);
        var root=GeneratedRoot;
        if(root==null)return;
        // Preserve sparse flavor/resource decorations over the uniform base terrain.
        for (int x=0;x<grid.MapWidth;x++)
        for (int y=0;y<grid.MapHeight;y++)
        {
            var cell=grid.GetCell(x,y);
            if(cell==null)continue;
            if(cell.Flavor!=grid.DefaultFlavor) CreateSurface(root,$"Flavor_{x}_{y}",cell.WorldPos,
                Vector2.one*grid.CellSize,grid.CellSize,ColorFor(cell.Flavor),1);
            if(cell.ScrapDensity>0) CreateSurface(root,$"Resource_{x}_{y}",cell.WorldPos,
                Vector2.one*grid.CellSize*.3f,grid.CellSize,new Color(1,.9f,0,.6f),2);
        }
    }
    public Color ColorFor(TileFlavor flavor) => flavor==TileFlavor.TechBase ? ColorTech :
        flavor==TileFlavor.FleshNest ? ColorFlesh : ColorWasteland;

    // The editor registers the returned hierarchy as one Undo operation.
    public GameObject Rebuild(RTSGridSystem grid)
    {
        if(grid==null || BaseTileSprite==null)return null;
        grid.ValidateSettings();
        var existing=GeneratedRoot;
        if(existing!=null)
        {
            if(Application.isPlaying){existing.gameObject.SetActive(false);existing.name="Retired_Grid_Tiles";Destroy(existing.gameObject);}
            else DestroyImmediate(existing.gameObject);
        }
        var root=new GameObject(GeneratedRootName);
        root.transform.SetParent(transform,false);
        root.transform.position=Vector3.zero; root.transform.rotation=Quaternion.identity;
        var scale=transform.lossyScale;
        root.transform.localScale=new Vector3(1/NonZero(scale.x),1/NonZero(scale.y),1/NonZero(scale.z));
        for(int x=0;x<grid.MapWidth;x+=ChunkSize)
        for(int y=0;y<grid.MapHeight;y+=ChunkSize)
        {
            int width=Mathf.Min(ChunkSize,grid.MapWidth-x),height=Mathf.Min(ChunkSize,grid.MapHeight-y);
            Vector3 center=grid.GridToWorld(x,y)+new Vector3((width-1)*grid.CellSize*.5f,(height-1)*grid.CellSize*.5f,0);
            CreateSurface(root.transform,$"Chunk_{x}_{y}",center,new Vector2(width,height)*grid.CellSize,grid.CellSize,ColorFor(grid.DefaultFlavor));
        }
        previewWidth=grid.MapWidth;previewHeight=grid.MapHeight;previewCellSize=grid.CellSize;
        previewOrigin=grid.GridOrigin;previewSprite=BaseTileSprite;previewColor=ColorFor(grid.DefaultFlavor);
        return root;
    }
    private static float NonZero(float value) => Mathf.Abs(value)<.0001f ? 1 : value;
    private void CreateSurface(Transform parent,string name,Vector3 center,Vector2 worldSize,float cell,Color color,int order=0)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);
        go.transform.position=new Vector3(center.x,center.y,1);
        var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=BaseTileSprite;renderer.color=color;
        renderer.sortingLayerName="Floor";renderer.sortingOrder=order;
        renderer.drawMode=SpriteDrawMode.Tiled;renderer.tileMode=SpriteTileMode.Continuous;
        // The sprite's pixel density must not change the logical cell's world size.
        Vector2 spriteSize=BaseTileSprite.bounds.size;
        go.transform.localScale=new Vector3(cell/NonZero(spriteSize.x),cell/NonZero(spriteSize.y),1);
        renderer.size=new Vector2(worldSize.x/cell*spriteSize.x,worldSize.y/cell*spriteSize.y);
        go.transform.position += new Vector3(center.x,center.y,1)-renderer.bounds.center;
    }
}
