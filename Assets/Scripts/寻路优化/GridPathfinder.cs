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

    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld)
    {
        var sys = RTSGridSystem.Instance;
        Vector2Int startGrid = sys.WorldToGrid(startWorld);
        Vector2Int endGrid = sys.WorldToGrid(endWorld);

        if (sys.GetCell(endGrid.x, endGrid.y).IsOccupied)
            endGrid = FindNearestWalkableCell(endGrid);

        List<Node> openList = new List<Node>();
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();
        openList.Add(new Node(startGrid));

        while (openList.Count > 0)
        {
            Node curr = openList[0];
            for (int i = 1; i < openList.Count; i++)
                if (openList[i].F < curr.F) curr = openList[i];

            openList.Remove(curr);
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
                if (!isStartNode && sys.GetCell(neighborPos.x, neighborPos.y).IsOccupied) continue;
                float moveCost = (curr.GridPos.x != neighborPos.x && curr.GridPos.y != neighborPos.y) ? 1.4f : 1f;
                float newG = curr.G + moveCost;
                Node neighborNode = openList.Find(n => n.GridPos == neighborPos);

                if (neighborNode == null)
                {
                    neighborNode = new Node(neighborPos) { G = newG, H = Vector2Int.Distance(neighborPos, endGrid), Parent = curr };
                    openList.Add(neighborNode);
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
            // 从远端向近端扫描，寻找最远的可见点
            for (int i = rawPath.Count - 1; i > current; i--)
            {
                if (IsLineClear(rawPath[current], rawPath[i]))
                {
                    simplified.Add(rawPath[i]);
                    current = i; // 跳过中间所有点
                    break;
                }
            }
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
        for (float d = step; d < dist; d += step)
        {
            Vector3 checkPoint = start + dir * d;
            Vector2Int gridIdx = sys.WorldToGrid(checkPoint);

            // 只有当检测点离开起点格子后，才执行阻挡判定
            if (gridIdx != sys.WorldToGrid(start))
            {
                if (sys.GetCell(gridIdx.x, gridIdx.y).IsOccupied) return false;
            }
        }
        return true;
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
                    if (RTSGridSystem.Instance.GetCell(next.x, next.y) != null && !RTSGridSystem.Instance.GetCell(next.x, next.y).IsOccupied)
                        return next;
                }
            }
        }
        return target;
    }
}