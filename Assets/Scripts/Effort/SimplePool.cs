using System.Collections.Generic;
using UnityEngine;

public static class SimplePool
{
    private static Dictionary<GameObject, Stack<GameObject>> poolDict = new Dictionary<GameObject, Stack<GameObject>>();

    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!poolDict.ContainsKey(prefab)) poolDict.Add(prefab, new Stack<GameObject>());

        GameObject obj;
        if (poolDict[prefab].Count > 0)
        {
            obj = poolDict[prefab].Pop();
            obj.transform.position = pos;
            obj.transform.rotation = rot;
            obj.SetActive(true);
        }
        else
        {
            obj = Object.Instantiate(prefab, pos, rot);
        }
        return obj;
    }

    public static void Despawn(GameObject prefab, GameObject instance)
    {
        if (!poolDict.ContainsKey(prefab)) poolDict.Add(prefab, new Stack<GameObject>());
        instance.SetActive(false);
        poolDict[prefab].Push(instance);
    }
}