// --- IResidentCarrier.cs ---
using UnityEngine;
using System.Collections.Generic;

public interface IResidentCarrier
{
    string GetCarrierName();
    int MaxStaffCapacity { get; } // 🌟 检查这里的属性名是否对齐
    List<ResidentData> GetStaffList();
    bool TryAddStaff(ResidentData data);
    void RemoveStaff(ResidentData data);
    Vector3 GetInteractionPoint();
}