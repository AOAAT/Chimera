using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private class Node
    {
        public Vector2Int GridPos;
        public Node Parent;
        public float G, H;
        public float F => G + H;
        public Node(Vector2Int pos) => GridPos = pos;
    }

    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld, bool allowNearbyTarget = true)
    {
        var sys = RTSGridSystem.Instance;
        if (sys == null || sys.CellSize <= 0f) return null;
        if (!allowNearbyTarget && !sys.TryWorldToGrid(endWorld, out _)) return null;
        Vector2Int startGrid = sys.WorldToGrid(startWorld);
        Vector2Int endGrid = sys.WorldToGrid(endWorld);

        if (!IsWalkable(endGrid))
        {
            if (!allowNearbyTarget) return null;
            endGrid = FindNearestWalkableCell(endGrid);
            if (!IsWalkable(endGrid)) return null;
        }

        List<Node> openList = new List<Node>();
        Dictionary<Vector2Int, Node> openNodes = new Dictionary<Vector2Int, Node>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();
        Node startNode = new Node(startGrid);
        openList.Add(startNode);
        openNodes.Add(startGrid, startNode);

        // 单次请求有明确上限，批量移动不会无界占用主线程。
        int remaining = 8192;
        while (openList.Count > 0 && remaining-- > 0)
        {
            Node curr = openList[0];
            for (int i = 1; i < openList.Count; i++)
                if (openList[i].F < curr.F) curr = openList[i];

            openList.Remove(curr);
            openNodes.Remove(curr.GridPos);
            closedList.Add(curr.GridPos);

            if (curr.GridPos == endGrid)
            {
                List<Vector3> rawPath = RetracePath(curr);

                // --- 👇【关键修复：消除抽搐】---
                // 核心：如果路径存在，强行把第 1 个点设为单位的“真实当前坐标”
                // 而不是格子中心点 WorldPos。
                if (rawPath.Count > 0)
                {
                    rawPath[0] = startWorld;
                }
                // ------------------------------

                return SimplifyPath(rawPath);
            }

            foreach (Vector2Int neighborPos in GetNeighbors(curr.GridPos))
            {
                if (neighborPos.x < 0 || neighborPos.x >= sys.MapWidth ||
        neighborPos.y < 0 || neighborPos.y >= sys.MapHeight) continue;

                if (closedList.Contains(neighborPos)) continue;

                // 🌟 加固 2：起点豁免逻辑
                // 如果这个格子就是起点，即便它被建筑占用了（单位刚好卡在里面），也允许通行，否则寻路会直接失败
                bool isStartNode = (neighborPos == startGrid);
                if (!isStartNode && !IsWalkable(neighborPos)) continue;
                if (curr.GridPos.x != neighborPos.x && curr.GridPos.y != neighborPos.y &&
                    (!IsWalkable(new Vector2Int(curr.GridPos.x, neighborPos.y)) ||
                     !IsWalkable(new Vector2Int(neighborPos.x, curr.GridPos.y)))) continue;
                float moveCost = (curr.GridPos.x != neighborPos.x && curr.GridPos.y != neighborPos.y) ? 1.4f : 1f;
                float newG = curr.G + moveCost;
                openNodes.TryGetValue(neighborPos, out Node neighborNode);

                if (neighborNode == null)
                {
                    neighborNode = new Node(neighborPos) { G = newG, H = Vector2Int.Distance(neighborPos, endGrid), Parent = curr };
                    openList.Add(neighborNode);
                    openNodes.Add(neighborPos, neighborNode);
                }
                else if (newG < neighborNode.G)
                {
                    neighborNode.G = newG;
                    neighborNode.Parent = curr;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 🌟 路径平滑算法 (String Pulling)
    /// </summary>
    private static List<Vector3> SimplifyPath(List<Vector3> rawPath)
    {
        if (rawPath.Count <= 2) return rawPath;

        List<Vector3> simplified = new List<Vector3>();
        simplified.Add(rawPath[0]); // 保留起点

        int current = 0;
        while (current < rawPath.Count - 1)
        {
            // 即使直线检测全部失败，也至少沿原始 A* 路径前进一格。
            int next = current + 1;
            // 从远端向近端扫描，寻找最远的可见点
            for (int i = Mathf.Min(rawPath.Count - 1, current + 32); i > current + 1; i--)
            {
                if (IsLineClear(rawPath[current], rawPath[i]))
                {
                    next = i;
                    break;
                }
            }
            simplified.Add(rawPath[next]);
            current = next;
        }
        return simplified;
    }

    /// <summary>
    /// 检查两点之间是否有建筑阻挡 (逻辑层扫描)
    /// </summary>
    private static bool IsLineClear(Vector3 start, Vector3 end)
    {
        var sys = RTSGridSystem.Instance;
        float dist = Vector3.Distance(start, end);
        Vector3 dir = (end - start).normalized;
        float step = sys.CellSize * 0.4f; // 步长稍微缩小，提高精度

        // 🌟 从起始点偏移一点点距离开始扫描，防止“自己撞到自己脚下的建筑”
        int samples = Mathf.CeilToInt(dist / Mathf.Max(0.001f, step));
        Vector2Int previous = sys.WorldToGrid(start);
        for (int sample = 1; sample <= samples; sample++)
        {
            Vector3 checkPoint = start + dir * Mathf.Min(sample * step, dist);
            Vector2Int gridIdx = sys.WorldToGrid(checkPoint);

            // 只有当检测点离开起点格子后，才执行阻挡判定
            if (gridIdx != sys.WorldToGrid(start))
            {
                if (!IsWalkable(gridIdx)) return false;
                if (previous.x != gridIdx.x && previous.y != gridIdx.y &&
                    (!IsWalkable(new Vector2Int(previous.x, gridIdx.y)) ||
                     !IsWalkable(new Vector2Int(gridIdx.x, previous.y)))) return false;
            }
            previous = gridIdx;
        }
        return true;
    }

    private static bool IsWalkable(Vector2Int cell)
    {
        GridCell value = RTSGridSystem.Instance.GetCell(cell.x, cell.y);
        return value != null && value.IsWalkable && !value.IsOccupied;
    }


    // 其余辅助方法 (GetNeighbors, RetracePath, FindNearestWalkableCell) 保持不变...
    private static List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        return new List<Vector2Int> {
            pos + Vector2Int.up, pos + Vector2Int.down, pos + Vector2Int.left, pos + Vector2Int.right,
            pos + new Vector2Int(1,1), pos + new Vector2Int(-1,1), pos + new Vector2Int(1,-1), pos + new Vector2Int(-1,-1)
        };
    }

    private static List<Vector3> RetracePath(Node endNode)
    {
        List<Vector3> path = new List<Vector3>();
        Node temp = endNode;
        while (temp != null)
        {
            path.Add(RTSGridSystem.Instance.GetCell(temp.GridPos.x, temp.GridPos.y).WorldPos);
            temp = temp.Parent;
        }
        path.Reverse();
        return path;
    }

    private static Vector2Int FindNearestWalkableCell(Vector2Int target)
    {
        for (int r = 1; r < 3; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int next = target + new Vector2Int(x, y);
                    if (IsWalkable(next))
                        return next;
                }
            }
        }
        return target;
    }
}
