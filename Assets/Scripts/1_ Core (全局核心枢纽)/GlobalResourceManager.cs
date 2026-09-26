using System;
using UnityEngine;

public class GlobalResourceManager : MonoBehaviour
{
    public static GlobalResourceManager Instance;
    public event Action OnResourceChanged;

    [Header("=== 基础储备 (一级资源) ===")]
    public float CurrentScrap = 0;
    public float CurrentBiomass = 0;
    public float CurrentManaStone = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start() => LogisticsManager.EnsureInstance();

    private void Update()
    {
        // 🚀 R键：补给协议 (调试用)
        if (Input.GetKeyDown(KeyCode.R))
        {
            AddResources(new ResourceSet(500, 200, 50));
            Debug.Log("<color=green>【补给】</color> 基础储备已注入。");
        }
    }

    public void AddResources(ResourceSet res)
    {
        if (!LogisticsKeys.Valid(res)) return;
        if (LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
        { LogisticsManager.Instance.DepositResources(res); return; }
        CurrentScrap += res.Scrap;
        CurrentBiomass += res.Biomass;
        CurrentManaStone += res.ManaStone;
        OnResourceChanged?.Invoke();
    }

    public bool CanAfford(ResourceSet cost)
    {
        return LogisticsKeys.Valid(cost) && CurrentScrap >= cost.Scrap &&
               CurrentBiomass >= cost.Biomass &&
               CurrentManaStone >= cost.ManaStone;
    }

    public bool TryConsume(ResourceSet cost)
    {
        if (LogisticsManager.Instance != null && LogisticsManager.Instance.Ready)
            return LogisticsManager.Instance.ConsumeResources(cost);
        if (!CanAfford(cost)) return false;

        CurrentScrap -= cost.Scrap;
        CurrentBiomass -= cost.Biomass;
        CurrentManaStone -= cost.ManaStone;
        OnResourceChanged?.Invoke();
        return true;
    }

    public void Refund(ResourceSet cost)
    {
        AddResources(cost);
    }

    public void SyncLogisticsTotals(float scrap, float biomass, float mana)
    {
        if (Mathf.Approximately(CurrentScrap, scrap) && Mathf.Approximately(CurrentBiomass, biomass) && Mathf.Approximately(CurrentManaStone, mana)) return;
        CurrentScrap = scrap; CurrentBiomass = biomass; CurrentManaStone = mana;
        OnResourceChanged?.Invoke();
    }

    public void RestoreResources(ResourceSaveData data)
    {
        if (data == null) return;
        CurrentScrap = data.Scrap;
        CurrentBiomass = data.Biomass;
        CurrentManaStone = data.ManaStone;
        OnResourceChanged?.Invoke();
    }
}
