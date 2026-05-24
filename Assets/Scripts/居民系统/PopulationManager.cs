using UnityEngine;
using System;
using System.Collections.Generic;

public class PopulationManager : MonoBehaviour
{
    public static PopulationManager Instance;
    public event Action OnPopulationChanged;

    [Header("=== 档案库与预制体 ===")]
    public ResidentIdentityLibrarySO IdentityLibrary;
    public GameObject ResidentPrefab;

    [Header("=== 人口实况账本 ===")]
    public int BaseMaxPopulation = 5;
    public List<ResidentData> TotalResidents = new List<ResidentData>();
    private int currentTotalMax;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 🌟 【核心删除】：删掉原有的 Update() 方法和 timer 变量
    // 因为招募时间现在由 HeadquartersBuilding 说了算

    public void RefreshMaxCapacity()
    {
        int bonus = 0;
        foreach (var building in BuildingBase.AllPlacedBuildings)
        {
            if (building is HousingBuilding housing) bonus += housing.CapacityProvided;
        }
        currentTotalMax = BaseMaxPopulation + bonus;
        OnPopulationChanged?.Invoke();
    }

    public int GetCurrentMaxCapacity()
    {
        if (currentTotalMax == 0) RefreshMaxCapacity();
        return currentTotalMax;
    }

    public bool IsFull()
    {
        return TotalResidents.Count >= GetCurrentMaxCapacity();
    }

    public void SpawnResidentAt(Vector3 spawnPos)
    {
        if (IsFull() || IdentityLibrary == null) return;

        ResidentData newData = IdentityLibrary.GenerateRandom();
        TotalResidents.Add(newData);

        GameObject go = Instantiate(ResidentPrefab, spawnPos, Quaternion.identity);
        ResidentEntity entity = go.GetComponent<ResidentEntity>();
        if (entity != null)
        {
            entity.Initialize(newData);
            entity.SetDestination(spawnPos + Vector3.right); // 走出门口
        }

        OnPopulationChanged?.Invoke();
        Debug.Log($"<color=green>【社会系统】</color> 新成员 {newData.ResidentName} 已成功入住。");
    }
}