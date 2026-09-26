using System.Collections.Generic;
using UnityEngine;

public enum ResidentWorkDomain { General, Tech, Flesh, Mana }

/// <summary>
/// Single source of truth for resident work output. Buildings and UI must use this
/// calculator so displayed and simulated productivity cannot diverge.
/// </summary>
public static class ResidentWorkCalculator
{
    public static float CalculateContribution(ResidentData resident, ResidentWorkDomain domain,
        ResidentIdentityLibrarySO identityLibrary)
    {
        if (resident == null) return 0f;

        float proficiency = GetProficiency(resident, domain);
        float disciplineMultiplier = Mathf.Lerp(0.9f, 1.1f, Mathf.Clamp01(resident.Discipline));
        float flatBonus = 0f;
        float traitMultiplier = 1f;

        if (identityLibrary != null && resident.TraitIDs != null)
        {
            foreach (string traitID in resident.TraitIDs)
            {
                ResidentTraitDefinitionSO trait = identityLibrary.GetTrait(traitID);
                if (trait == null) continue;
                flatBonus += trait.FlatProductivityBonus;
                traitMultiplier *= Mathf.Max(0f, trait.ProductivityMultiplier);
            }
        }

        return Mathf.Max(0.1f, (proficiency * disciplineMultiplier + flatBonus) * traitMultiplier);
    }

    public static string GetTraitSummary(ResidentData resident, ResidentIdentityLibrarySO identityLibrary)
    {
        if (resident == null || resident.TraitIDs == null || resident.TraitIDs.Count == 0) return "无特性";
        List<string> names = new List<string>();
        foreach (string traitID in resident.TraitIDs)
        {
            ResidentTraitDefinitionSO trait = identityLibrary != null ? identityLibrary.GetTrait(traitID) : null;
            names.Add(trait != null ? trait.DisplayName : traitID);
        }
        return string.Join("、", names);
    }

    private static float GetProficiency(ResidentData resident, ResidentWorkDomain domain)
    {
        switch (domain)
        {
            case ResidentWorkDomain.Tech: return resident.TechProficiency;
            case ResidentWorkDomain.Flesh: return resident.FleshProficiency;
            case ResidentWorkDomain.Mana: return resident.ManaProficiency;
            default: return (resident.TechProficiency + resident.FleshProficiency + resident.ManaProficiency) / 3f;
        }
    }
}
