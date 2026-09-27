using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ComponentQuality
{
    Crude = 0,
    Standard = 1,
    Fine = 2,
    Superior = 3,
    Masterwork = 4
}

[Serializable]
public class ComponentAffixInstance
{
    public string AffixID;
    public string DisplayName;
    [TextArea] public string Description;
    public List<StatEntry> Modifiers = new List<StatEntry>();
}

public sealed class ComponentAffixRule
{
    public string ID;
    public string Name;
    public string Description;
    public ComponentType[] AllowedTypes;
    public List<StatEntry> Modifiers;
}

/// <summary>
/// Stable built-in affix definitions for the first quality-system version.
/// The instance stores the rolled values, so later balance changes do not rewrite old equipment.
/// </summary>
public static class ComponentAffixCatalog
{
    private static readonly List<ComponentAffixRule> Rules = new List<ComponentAffixRule>
    {
        Rule("AFFIX_REINFORCED", "强化结构", "额外强化承力结构，提高组件提供的生命。",
            new[] { ComponentType.Core, ComponentType.Movement, ComponentType.Support, ComponentType.Factory },
            Multiplier(StatType.AddedHP, 1.12f)),
        Rule("AFFIX_LIGHTWEIGHT", "轻量化", "减少组件质量，但会略微削弱其结构强度。", null,
            Multiplier(StatType.AddedMass, 0.85f), Multiplier(StatType.AddedHP, 0.95f)),
        Rule("AFFIX_OVERCHARGED", "高压供能", "提高武器伤害，同时牺牲少量攻击速度。",
            new[] { ComponentType.Weapon }, Multiplier(StatType.MinDamage, 1.12f),
            Multiplier(StatType.MaxDamage, 1.12f), Multiplier(StatType.AttackSpeed, 0.95f)),
        Rule("AFFIX_CALIBRATED", "精密校准", "提高暴击率与弹体速度。",
            new[] { ComponentType.Weapon }, Additive(StatType.CriticalChance, 0.05f),
            Multiplier(StatType.ProjectileSpeed, 1.1f)),
        Rule("AFFIX_RESPONSIVE", "响应式传动", "提高攻击与驱动系统的响应速度。",
            new[] { ComponentType.Weapon, ComponentType.Movement },
            Multiplier(StatType.AttackSpeed, 1.1f), Multiplier(StatType.EnginePower, 1.08f)),
        Rule("AFFIX_ARMORED", "装甲封装", "提高组件提供的护甲，但会增加质量。",
            new[] { ComponentType.Core, ComponentType.Movement, ComponentType.Support, ComponentType.Factory },
            Multiplier(StatType.AddedAP, 1.12f), Multiplier(StatType.AddedMass, 1.06f))
    };

    public static IReadOnlyList<ComponentAffixRule> GetCompatible(ComponentDataSO component)
    {
        if (component == null) return Array.Empty<ComponentAffixRule>();
        return Rules.Where(rule => rule.AllowedTypes == null || rule.AllowedTypes.Length == 0 ||
            rule.AllowedTypes.Contains(component.Type)).ToList();
    }

    private static ComponentAffixRule Rule(string id, string name, string description,
        ComponentType[] allowedTypes, params StatEntry[] modifiers)
    {
        return new ComponentAffixRule
        {
            ID = id,
            Name = name,
            Description = description,
            AllowedTypes = allowedTypes,
            Modifiers = modifiers.ToList()
        };
    }

    private static StatEntry Additive(StatType stat, float value) => new StatEntry
        { StatID = stat, Value = value, ModType = BuffModifierType.Additive };

    private static StatEntry Multiplier(StatType stat, float value) => new StatEntry
        { StatID = stat, Value = value, ModType = BuffModifierType.Multiplier };
}

public static class ComponentQualityUtility
{
    public static string GetName(ComponentQuality quality)
    {
        switch (quality)
        {
            case ComponentQuality.Crude: return "粗制";
            case ComponentQuality.Fine: return "优良";
            case ComponentQuality.Superior: return "精制";
            case ComponentQuality.Masterwork: return "杰作";
            default: return "标准";
        }
    }

    public static Color GetColor(ComponentQuality quality)
    {
        switch (quality)
        {
            case ComponentQuality.Crude: return new Color32(186, 197, 210, 255);
            case ComponentQuality.Fine: return new Color32(116, 221, 159, 255);
            case ComponentQuality.Superior: return new Color32(132, 194, 255, 255);
            case ComponentQuality.Masterwork: return new Color32(255, 207, 111, 255);
            default: return Color.white;
        }
    }

    public static string GetSummary(ComponentQuality quality, float qualityScore)
    {
        return $"{GetName(quality)} {qualityScore.ToString("+0.0%;-0.0%;0.0%")}";
    }
}

/// <summary>Creates a deterministic physical component from a completed production task.</summary>
public static class ComponentQualityGenerator
{
    public static InstancedComponent Create(ComponentDataSO definition, int mark, int seed,
        float craftsmanship, IList<string> makerIDs, string buildingID)
    {
        InstancedComponent result = new InstancedComponent(definition, mark)
        {
            CraftSeed = seed,
            Craftsmanship = craftsmanship,
            CraftedAtBuildingID = buildingID ?? string.Empty,
            CraftedByResidentIDs = makerIDs != null ? new List<string>(makerIDs) : new List<string>()
        };

        System.Random random = new System.Random(seed);
        result.Quality = RollQuality(random, craftsmanship);
        result.QualityScore = GetQualityScore(result.Quality, random);
        result.RolledStats = RollQualityStats(definition, mark, result.QualityScore);
        result.Affixes = RollAffixes(definition, mark, result.Quality, random);
        return result;
    }

    /// <summary>
    /// Gives components created by old saves, inspector data, or compatibility APIs a stable roll.
    /// Existing generated components are left untouched.
    /// </summary>
    public static void EnsureGenerated(InstancedComponent component, float fallbackCraftsmanship = 0f)
    {
        if (component?.BaseData == null) return;

        component.RolledStats = component.RolledStats ?? new List<StatEntry>();
        component.Affixes = component.Affixes ?? new List<ComponentAffixInstance>();
        component.CraftedByResidentIDs = component.CraftedByResidentIDs ?? new List<string>();
        bool alreadyGenerated = component.CraftSeed != 0 ||
            Mathf.Abs(component.QualityScore) > 0.00001f ||
            component.RolledStats.Count > 0 || component.Affixes.Count > 0 ||
            component.Quality != ComponentQuality.Standard;
        if (alreadyGenerated) return;

        int seed = StableSeed(component.InstanceID);
        float craftsmanship = component.Craftsmanship != 0f
            ? component.Craftsmanship : fallbackCraftsmanship;
        InstancedComponent generated = Create(component.BaseData, component.CurrentMark, seed,
            craftsmanship, component.CraftedByResidentIDs, component.CraftedAtBuildingID);
        component.CraftSeed = seed;
        component.Craftsmanship = craftsmanship;
        component.Quality = generated.Quality;
        component.QualityScore = generated.QualityScore;
        component.RolledStats = generated.RolledStats;
        component.Affixes = generated.Affixes;
    }

    public static int StableSeed(string identity)
    {
        unchecked
        {
            uint hash = 2166136261;
            string source = string.IsNullOrWhiteSpace(identity) ? Guid.NewGuid().ToString("N") : identity;
            for (int i = 0; i < source.Length; i++)
            {
                hash ^= source[i];
                hash *= 16777619;
            }
            int seed = (int)hash;
            return seed == 0 ? 1 : seed;
        }
    }

    private static ComponentQuality RollQuality(System.Random random, float craftsmanship)
    {
        float roll = (float)random.NextDouble() * 100f;
        roll += Mathf.Clamp(craftsmanship - 1f, -1f, 5f) * 8f;
        if (roll < 12f) return ComponentQuality.Crude;
        if (roll < 65f) return ComponentQuality.Standard;
        if (roll < 88f) return ComponentQuality.Fine;
        if (roll < 98f) return ComponentQuality.Superior;
        return ComponentQuality.Masterwork;
    }

    private static float GetQualityScore(ComponentQuality quality, System.Random random)
    {
        float center;
        switch (quality)
        {
            case ComponentQuality.Crude: center = -0.055f; break;
            case ComponentQuality.Fine: center = 0.04f; break;
            case ComponentQuality.Superior: center = 0.08f; break;
            case ComponentQuality.Masterwork: center = 0.13f; break;
            default: center = 0f; break;
        }
        return center + ((float)random.NextDouble() * 0.02f - 0.01f);
    }

    private static List<StatEntry> RollQualityStats(ComponentDataSO definition, int mark, float score)
    {
        List<StatEntry> result = new List<StatEntry>();
        ComponentModelData model = definition != null ? definition.GetModelData(mark) : null;
        if (model?.Stats == null) return result;

        foreach (StatEntry source in model.Stats)
        {
            if (source == null || source.Value == 0f || !IsQualityScalable(source.StatID)) continue;
            float direction = source.StatID == StatType.AddedMass || source.StatID == StatType.MinRange ? -1f : 1f;
            result.Add(new StatEntry
            {
                StatID = source.StatID,
                Value = source.Value * score * direction,
                ModType = BuffModifierType.Additive
            });
        }
        return result;
    }

    private static List<ComponentAffixInstance> RollAffixes(ComponentDataSO definition, int mark,
        ComponentQuality quality, System.Random random)
    {
        int count = quality == ComponentQuality.Masterwork ? 2 :
            quality == ComponentQuality.Superior || quality == ComponentQuality.Fine ? 1 :
            quality == ComponentQuality.Standard && random.NextDouble() < 0.08 ? 1 : 0;
        ComponentModelData model = definition != null ? definition.GetModelData(mark) : null;
        HashSet<StatType> existingStats = model?.Stats != null
            ? new HashSet<StatType>(model.Stats.Where(stat => stat != null && stat.Value != 0f).Select(stat => stat.StatID))
            : new HashSet<StatType>();
        List<ComponentAffixRule> candidates = ComponentAffixCatalog.GetCompatible(definition)
            .Where(rule => rule.Modifiers.Any(modifier => IsBeneficialModifier(modifier) &&
                existingStats.Contains(modifier.StatID))).ToList();
        List<ComponentAffixInstance> result = new List<ComponentAffixInstance>();

        while (count-- > 0 && candidates.Count > 0)
        {
            int index = random.Next(candidates.Count);
            ComponentAffixRule selected = candidates[index];
            candidates.RemoveAt(index);
            result.Add(new ComponentAffixInstance
            {
                AffixID = selected.ID,
                DisplayName = selected.Name,
                Description = selected.Description,
                Modifiers = selected.Modifiers.Select(CloneStat).ToList()
            });
        }
        return result;
    }

    private static bool IsBeneficialModifier(StatEntry modifier)
    {
        if (modifier == null) return false;
        bool lowerIsBetter = modifier.StatID == StatType.AddedMass || modifier.StatID == StatType.MinRange;
        if (modifier.ModType == BuffModifierType.Multiplier)
            return lowerIsBetter ? modifier.Value < 1f : modifier.Value > 1f;
        return lowerIsBetter ? modifier.Value < 0f : modifier.Value > 0f;
    }

    private static bool IsQualityScalable(StatType stat)
    {
        return stat != StatType.MultiShotCount;
    }

    private static StatEntry CloneStat(StatEntry source) => new StatEntry
        { StatID = source.StatID, Value = source.Value, ModType = source.ModType };
}

/// <summary>Single source of truth for component values used by UI and combat.</summary>
public static class ComponentStatResolver
{
    public static List<StatEntry> Resolve(InstancedComponent component)
    {
        Dictionary<StatType, float> additive = new Dictionary<StatType, float>();
        Dictionary<StatType, float> multipliers = new Dictionary<StatType, float>();
        if (component?.BaseData == null) return new List<StatEntry>();

        ComponentModelData model = component.BaseData.GetModelData(component.CurrentMark);
        Apply(model?.Stats, additive, multipliers);
        Apply(component.RolledStats, additive, multipliers);
        if (component.Affixes != null)
            foreach (ComponentAffixInstance affix in component.Affixes)
                Apply(affix?.Modifiers, additive, multipliers);

        return additive.Keys.Union(multipliers.Keys)
            .Select(stat => new StatEntry
            {
                StatID = stat,
                Value = additive.TryGetValue(stat, out float value) ? value * GetMultiplier(multipliers, stat) : 0f,
                ModType = BuffModifierType.Additive
            }).ToList();
    }

    public static float GetValue(InstancedComponent component, StatType stat)
    {
        StatEntry entry = Resolve(component).Find(item => item.StatID == stat);
        return entry != null ? entry.Value : 0f;
    }

    private static void Apply(IEnumerable<StatEntry> stats, Dictionary<StatType, float> additive,
        Dictionary<StatType, float> multipliers)
    {
        if (stats == null) return;
        foreach (StatEntry stat in stats)
        {
            if (stat == null) continue;
            if (stat.ModType == BuffModifierType.Multiplier)
                multipliers[stat.StatID] = GetMultiplier(multipliers, stat.StatID) * stat.Value;
            else
                additive[stat.StatID] = (additive.TryGetValue(stat.StatID, out float value) ? value : 0f) + stat.Value;
        }
    }

    private static float GetMultiplier(Dictionary<StatType, float> multipliers, StatType stat) =>
        multipliers.TryGetValue(stat, out float value) ? value : 1f;
}
