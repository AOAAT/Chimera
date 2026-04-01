using UnityEngine;

public abstract class EventCondition : ScriptableObject
{
    // 返回 true 代表条件满足；返回 false 代表不满足，并输出红字原因
    public abstract bool Evaluate(out string failReason);
}

public abstract class EventAction : ScriptableObject
{
    // 执行原子行为
    public abstract void Execute();
}