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
    public int BaseMaxPopulation = 5; // 基地自带基础容量
    public List<ResidentData> TotalResidents = new List<ResidentData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 计算当前总人口上限
    public int GetCurrentMaxCapacity()
    {
        return BaseMaxPopulation;
    }

    public bool IsFull()
    {
        return TotalResidents.Count >= GetCurrentMaxCapacity();
    }

    /// <summary>
    /// 核心产出接口：在指定位置生成居民
    /// </summary>
    public void SpawnResidentAt(Vector3 spawnPos)
    {
        if (IsFull()) return;

        // 🌟【核心物理修复】：强行将传入的出生点 Z 轴归零！
        // 这样可以彻底切断它从主建筑（HeadquartersBuilding）继承过来的 Z 轴错位
        spawnPos.z = 0f;

        // 1. 生成数据
        ResidentData newData = IdentityLibrary.GenerateRandom();
        TotalResidents.Add(newData);

        // 2. 实例化实体 (此时带有绝对正确的 Z=0 坐标)
        GameObject go = Instantiate(ResidentPrefab, spawnPos, Quaternion.identity);
        ResidentEntity entity = go.GetComponent<ResidentEntity>();

        // 🌟【双重保险】：防止 Instantiate 时由于父级或预制体残留导致 Z 轴再次漂移
        if (go != null)
        {
            go.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
        }

        if (entity != null)
        {
            entity.Initialize(newData);
            // 3. 让他自动往前走一步，腾出门口位置 (向右偏移 1m)
            entity.SetDestination(new Vector2(spawnPos.x + 1f, spawnPos.y));
        }

        OnPopulationChanged?.Invoke();
        Debug.Log($"<color=green>【电台接收成功】</color> 幸存者 {newData.ResidentName} 已抵达基地门口，物理坐标已校准至 Z=0。");
    }
}