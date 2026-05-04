// --- 请更新 EventNodeSO.cs ---
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EventOption
{
    [Header("选项文本")]
    public string OptionText = "[拾荒] 翻找废弃的机甲残骸";
    public string FlavorText = "风味描述...";

    [Header("ECA 逻辑判定")]
    public List<EventCondition> Conditions = new List<EventCondition>();
    public List<EventAction> Actions = new List<EventAction>();

    [Header("连环事件跳转 (可选)")]
    public EventNodeSO NextEventNode;
}

[CreateAssetMenu(fileName = "NewEvent", menuName = "Chimera Protocol/3. 宏观控制/文字事件 (Event Node)")]
public class EventNodeSO : ScriptableObject
{
    [Header("=== 全局出现门槛 (New!) ===")]
    [Tooltip("只有满足这些条件，该事件才会被洗入随机池供玩家踩到")]
    public List<EventCondition> AppearanceConditions = new List<EventCondition>();

    [Header("=== 基础文案 ===")]
    public string EventTitle = "未知的无线电信号";
    [TextArea(5, 10)]
    public string EventDescription = "你接收到一段断断续续的无线电波...";
    public Sprite EventIllustration;

    [Header("=== 交互选项 ===")]
    public List<EventOption> Options = new List<EventOption>();
}