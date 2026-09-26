using UnityEngine;

[CreateAssetMenu(fileName = "NewResidentTrait", menuName = "Chimera Protocol/居民系统/居民特性")]
public class ResidentTraitDefinitionSO : ScriptableObject
{
    [Header("=== 稳定身份 ===")]
    [Tooltip("写入存档的稳定 ID。发布后不要修改或复用。")]
    public string TraitID = "TRAIT_000";
    public string DisplayName = "新特性";
    [TextArea] public string Description;

    [Header("=== 工作修正 ===")]
    [Tooltip("先增加固定生产力，再乘以此倍率。1 表示不修正。")]
    public float ProductivityMultiplier = 1f;
    public float FlatProductivityBonus = 0f;
}
