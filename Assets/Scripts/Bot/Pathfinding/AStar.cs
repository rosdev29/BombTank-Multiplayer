using System.Collections.Generic;
using UnityEngine;

public static class AStar
{
    public static List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos)
    {
        PathfindingGrid grid = PathfindingGrid.Instance;
        if (grid == null) return null;

        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        if (!startNode.Walkable || !targetNode.Walkable)
        {
            // Neu target nam trong tuong, tim node gan nhat co the di duoc xung quanh no
            if (!targetNode.Walkable)
            {
                Node validNeighbor = GetNearestWalkableNode(grid, targetNode);
                if (validNeighbor != null)
                {
                    targetNode = validNeighbor;
                }
                else
                {
                    return null; // Khong the den target
                }
            }
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || 
                   (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.Walkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                // Chi phi di chuyen cheo la 14, di thang la 10
                int newMovementCostToNeighbor = currentNode.GCost + GetDistance(currentNode, neighbor) + neighbor.Penalty;
                if (newMovementCostToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
                {
                    neighbor.GCost = newMovementCostToNeighbor;
                    neighbor.HCost = GetDistance(neighbor, targetNode);
                    neighbor.Parent = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null; // Khong tim thay duong
    }

    private static Node GetNearestWalkableNode(PathfindingGrid grid, Node targetNode)
    {
        List<Node> neighbors = grid.GetNeighbors(targetNode);
        foreach (Node n in neighbors)
        {
            if (n.Walkable) return n;
        }
        return null;
    }

    private static List<Vector2> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2> path = new List<Vector2>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.WorldPosition);
            currentNode = currentNode.Parent;
        }

        // Lat nguoc lai de duoc duong di tu start -> end
        path.Reverse();
        return path;
    }

    private static int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}
