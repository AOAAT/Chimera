using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
public sealed class UIGridColumns : MonoBehaviour
{
    [Min(1)] public int Columns = 2;
    public float MaxCellWidth = 172;
    public float HeightRatio = 1;
    private void OnEnable() => Refresh();
    private void OnRectTransformDimensionsChange() => Refresh();
    public void Refresh()
    {
        var grid = GetComponent<GridLayoutGroup>();
        if(grid == null) return;
        int columns = Mathf.Max(1,Columns);
        float width = ((RectTransform)transform).rect.width-grid.padding.horizontal-grid.spacing.x*(columns-1);
        float cell = Mathf.Clamp(width/columns,32,MaxCellWidth);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
        var size = new Vector2(cell,cell*HeightRatio);
        if((grid.cellSize-size).sqrMagnitude > .01f) grid.cellSize = size;
    }
}
