using UnityEngine;

public enum BuildingCategory { Research, Energy, Unit, Defense }

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Chimera Protocol/建筑图纸")]
public class BuildingDataSO : ScriptableObject
{
    public string BuildingName;
    public BuildingCategory Category;
    public Sprite Icon;
    public GameObject Prefab;

    [Header("=== 预留接口 (验证期暂不生效) ===")]
    public float BuildTime = 0f;
    public int GoldCost = 0;
}