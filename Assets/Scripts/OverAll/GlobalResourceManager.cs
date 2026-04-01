using System;
using UnityEngine;

public class GlobalResourceManager : MonoBehaviour
{
    public static GlobalResourceManager Instance;

    public event Action OnResourceChanged;

    [Header("=== 全局状态 ===")]
    public int MaxSAN = 100;
    public int CurrentSAN = 100;

    public int Materials = 0; // 废料/材料
    public int DaysSurvived = 1; // 游戏经过的天数

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ModifySAN(int amount)
    {
        CurrentSAN = Mathf.Clamp(CurrentSAN + amount, 0, MaxSAN);
        Debug.Log($"【资源系统】SAN 值变动: {amount}，当前 SAN: {CurrentSAN}/{MaxSAN}");
        OnResourceChanged?.Invoke();

        if (CurrentSAN <= 0)
        {
            Debug.LogError("【游戏结束】指挥官理智清零，奇美拉小队彻底覆灭！");
            // TODO: 未来在这里弹 Game Over 面板
        }
    }

    public void ModifyMaterials(int amount)
    {
        Materials = Mathf.Max(0, Materials + amount);
        Debug.Log($"【资源系统】废料变动: {amount}，当前废料总计: {Materials}");
        OnResourceChanged?.Invoke();
    }

    public void AdvanceDay()
    {
        DaysSurvived++;
        Debug.Log($"<color=#00FFFF>【时间流逝】</color> 黎明降临。小队已存活 {DaysSurvived} 天。");
        OnResourceChanged?.Invoke();
    }
}