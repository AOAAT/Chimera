using System.Collections.Generic;
using UnityEngine;

public static class SimplePool
{
    private static Dictionary<GameObject, Stack<GameObject>> poolDict = new Dictionary<GameObject, Stack<GameObject>>();

    // --- 👇【核心新增】：彻底清空池子 ---
    public static void ClearPool()
    {
        foreach (var stack in poolDict.Values)
        {
            stack.Clear();
        }
        poolDict.Clear();
        Debug.Log("<color=yellow>【对象池】</color> 静态缓存已清空，旧场景引用已释放。");
    }

    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null; // 防呆

        if (!poolDict.ContainsKey(prefab)) poolDict.Add(prefab, new Stack<GameObject>());

        GameObject obj = null;

        // --- 👇【核心加固】：循环检查，直到拿到一个“活着的”物体 ---
        while (poolDict[prefab].Count > 0)
        {
            obj = poolDict[prefab].Pop();
            if (obj != null) // 检查这个物体是否还在内存中
            {
                obj.transform.position = pos;
                obj.transform.rotation = rot;
                obj.SetActive(true);
                return obj;
            }
        }

        // 如果池子里全是死物或者空了，则生成新的
        obj = Object.Instantiate(prefab, pos, rot);
        return obj;
    }

    public static void Despawn(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;

        if (!poolDict.ContainsKey(prefab)) poolDict.Add(prefab, new Stack<GameObject>());

        instance.SetActive(false);
        poolDict[prefab].Push(instance);
    }
}