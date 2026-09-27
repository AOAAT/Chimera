using System.Collections.Generic;
using UnityEngine;

public class MechEnergyVisuals : MonoBehaviour
{
    public GameObject ConduitPrefab; // 预制体，带 LineRenderer 和 MechEnergyConduit 脚本
    private List<MechEnergyConduit> conduits = new List<MechEnergyConduit>();
    private Transform coreTransform;

    public void SetupConduits(Transform core, List<Transform> sockets)
    {
        // 清理旧线
        foreach (var c in conduits) if (c != null) Destroy(c.gameObject);
        conduits.Clear();

        coreTransform = core;
        if (coreTransform == null || ConduitPrefab == null) return;

        foreach (var socket in sockets)
        {
            if (socket == coreTransform) continue;
            GameObject obj = Instantiate(ConduitPrefab, transform);
            var script = obj.GetComponent<MechEnergyConduit>();
            script.Initialize(coreTransform, socket);
            conduits.Add(script);
        }
    }

    // 提供给 ECA 积木调用的接口
    public void PulseAll()
    {
        foreach (var c in conduits) c.TriggerPulse();
    }
}