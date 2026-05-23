using System.Collections.Generic;
using UnityEngine;

public static class ConnectivityManager
{
    /// <summary>
    /// 核心算法：洪水填充。从地图最右侧（公海）向内搜索所有可达的空格子。
    /// </summary>
    /// <param name="tempOccupiedCells">当前正在模拟放置的格子（幽灵建筑占用的地方）</param>
    public static HashSet<Vector2Int> GetAccessibleArea(HashSet<Vector2Int> tempOccupiedCells)
    {
        var sys = RTSGridSystem.Instance;
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        // 1. 定义起点：地图最右侧的一整列（公海入口）
        int startX = sys.MapWidth - 1;
        for (int y = 0; y < sys.MapHeight; y++)
        {
            Vector2Int startPos = new Vector2Int(startX, y);
            // 如果边缘没被建筑堵死，则作为洪水起点
            if (!sys.GetCell(startX, y).IsOccupied && !tempOccupiedCells.Contains(startPos))
            {
                queue.Enqueue(startPos);
                reachable.Add(startPos);
            }
        }

        // 2. BFS 扩散
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();

            foreach (var dir in directions)
            {
                Vector2Int next = curr + dir;

                // 边界检查
                if (next.x < 0 || next.x >= sys.MapWidth || next.y < 0 || next.y >= sys.MapHeight) continue;

                // 逻辑检查：如果该格没被访问过，且逻辑上是空的（既没有既定建筑，也不是当前的幽灵建筑）
                if (!reachable.Contains(next) && !sys.GetCell(next.x, next.y).IsOccupied && !tempOccupiedCells.Contains(next))
                {
                    reachable.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return reachable;
    }
}