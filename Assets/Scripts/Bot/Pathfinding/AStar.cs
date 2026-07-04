using System.Collections.Generic;
using UnityEngine;

public static class AStar
{
    // Kích thước heap tối đa = toàn bộ node trên grid (80×80 = 6400)
    // Cấp phát 1 lần, tái dùng qua các lần FindPath để tránh GC.
    private static MinHeap<Node>  _openHeap    = new MinHeap<Node>(8192);
    private static HashSet<Node>  _openLookup  = new HashSet<Node>();
    private static HashSet<Node>  _closedSet   = new HashSet<Node>();
    private static List<Node>     _dirtyNodes  = new List<Node>();

    public static List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos)
    {
        PathfindingGrid grid = PathfindingGrid.Instance;
        if (grid == null) return null;

        Node startNode  = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        if (!startNode.Walkable)
        {
            startNode = GetNearestWalkableNode(grid, startNode);
            if (startNode == null) return null;
        }

        if (!targetNode.Walkable)
        {
            targetNode = GetNearestWalkableNode(grid, targetNode);
            if (targetNode == null) return null;
        }

        // Reset trạng thái từ lần trước
        ResetDirtyNodes();
        _openHeap.Clear();
        _openLookup.Clear();
        _closedSet.Clear();

        // Khởi tạo start node
        startNode.ResetPathData();
        startNode.GCost = 0;
        startNode.HCost = GetDistance(startNode, targetNode);
        MarkDirty(startNode);

        _openHeap.Push(startNode);
        _openLookup.Add(startNode);

        while (_openHeap.Count > 0)
        {
            Node current = _openHeap.Pop();
            _openLookup.Remove(current);
            _closedSet.Add(current);

            if (current == targetNode)
                return RetracePath(startNode, targetNode);

            foreach (Node neighbor in grid.GetNeighbors(current))
            {
                if (!neighbor.Walkable || _closedSet.Contains(neighbor))
                    continue;

                // g = chi phí thực từ start đến neighbor qua current
                int moveCost = current.GCost + GetDistance(current, neighbor) + neighbor.Penalty;

                bool inOpen = _openLookup.Contains(neighbor);

                if (moveCost < neighbor.GCost)
                {
                    // Lần đầu tiếp cận node này → reset dữ liệu cũ
                    if (!inOpen)
                    {
                        neighbor.ResetPathData();
                        MarkDirty(neighbor);
                    }

                    neighbor.GCost  = moveCost;
                    neighbor.HCost  = GetDistance(neighbor, targetNode);
                    neighbor.Parent = current;

                    if (inOpen)
                    {
                        // FCost giảm → đẩy lên trên heap
                        _openHeap.UpdateItem(neighbor);
                    }
                    else
                    {
                        _openHeap.Push(neighbor);
                        _openLookup.Add(neighbor);
                    }
                }
            }
        }

        return null; // Không tìm thấy đường
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void MarkDirty(Node node)
    {
        _dirtyNodes.Add(node);
    }

    private static void ResetDirtyNodes()
    {
        foreach (Node n in _dirtyNodes)
            n.ResetPathData();
        _dirtyNodes.Clear();
    }

    private static Node GetNearestWalkableNode(PathfindingGrid grid, Node startNode)
    {
        Queue<Node>   queue   = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        int maxDepth = 4;
        int depth    = 0;

        while (queue.Count > 0 && depth <= maxDepth)
        {
            int levelSize = queue.Count;
            for (int i = 0; i < levelSize; i++)
            {
                Node current = queue.Dequeue();
                if (current.Walkable) return current;

                foreach (Node neighbor in grid.GetNeighbors(current))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            depth++;
        }
        return null;
    }

    private static List<Vector2> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2> path = new List<Vector2>();
        Node current = endNode;

        while (current != startNode && current != null)
        {
            path.Add(current.WorldPosition);
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    // Octile distance (tối ưu hơn Euclidean cho grid 8 hướng)
    private static int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);

        if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}
