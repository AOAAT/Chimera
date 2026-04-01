using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EventOption
{
    [Header("选项文本")]
    public string OptionText = "[拾荒] 翻找废弃的机甲残骸";

    [Tooltip("悬停或显示在下方的补充风味描述")]
    public string FlavorText = "可能含有少量废料，但也可能惊醒寄生虫...";

    [Header("ECA 逻辑判定")]
    [Tooltip("必须满足所有条件才能点击")]
    public List<EventCondition> Conditions = new List<EventCondition>();

    [Tooltip("点击后执行的所有效果")]
    public List<EventAction> Actions = new List<EventAction>();

    [Header("连环事件跳转 (可选)")]
    [Tooltip("如果填了，点击后不会回大地图，而是跳转到下一个事件（比如打开了保险箱后的文本）")]
    public EventNodeSO NextEventNode;
}

[CreateAssetMenu(fileName = "NewEvent", menuName = "Chimera Protocol/Event Node (文字事件)")]
public class EventNodeSO : ScriptableObject
{
    public string EventTitle = "未知的无线电信号";

    [TextArea(5, 10)]
    public string EventDescription = "你接收到一段断断续续的无线电波...";

    public Sprite EventIllustration; // 事件的大幅插画

    public List<EventOption> Options = new List<EventOption>();
}