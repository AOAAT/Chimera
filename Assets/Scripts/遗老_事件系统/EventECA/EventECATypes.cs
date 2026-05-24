public enum EventResourceType
{
    CurrentSAN,
    MaxSAN,
    CurrentCP,         // 指挥点
    MaxCP,             // 指挥点上限
    MapDepth           // 当前探索层数 (只读)
}

public enum ComparisonType
{
    GreaterThan,
    LessThan,
    InRange
}