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
            entity.Initialize(newData, IdentityLibrary.DefaultResidentHP);
            entity.SetDestination(spawnPos + Vector3.right); // 走出门口
        }

        OnPopulationChanged?.Invoke();
        Debug.Log($"<color=green>【社会系统】</color> 新成员 {newData.ResidentName} 已成功入住。");
    }

    public void ExileResident(ResidentEntity entity)
    {
        if (entity == null) return;

        // 1. 从逻辑列表中移除数据
        if (TotalResidents.Contains(entity.MyData))
        {
            TotalResidents.Remove(entity.MyData);
        }

        // 2. 物理销毁
        Destroy(entity.gameObject);

        // 3. 触发人口变动事件 (通知顶部 HUD 刷新)
        OnPopulationChanged?.Invoke();

        Debug.Log($"<color=red>【社会放逐】</color> 居民 {entity.MyData.ResidentName} 已被移出基地。");
    }

    public void NotifyResidentDeath(ResidentEntity entity)
    {
        if (entity == null || entity.MyData == null) return;

        // 1. 从总人口名单中移除其“灵魂数据”
        if (TotalResidents.Contains(entity.MyData))
        {
            TotalResidents.Remove(entity.MyData);
            Debug.Log($"<color=red>【人口减损】</color> 居民 {entity.MyData.ResidentName} 已阵亡，释放 1 名人口空间。");
        }

        // 2. 触发事件，让顶部的 [人口: 4/5] UI 实时刷新
        OnPopulationChanged?.Invoke();

        // 💡 提示：这里不需要手动 Destroy，逻辑交由 Entity 自己处理
    }
}