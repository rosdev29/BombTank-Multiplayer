using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinding : MonoBehaviour
{
    public static AStarPathfinding Instance { get; private set; }
    public LayerMask layerVatCan;

    private void Awake()
    {
        // [SYNC] God-tier AI update synced to IDE
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (layerVatCan.value == 0) layerVatCan = LayerMask.GetMask("Default");
    }

    public List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos, Vector2? threatPos = null)
    {
        if (GridManager.Instance == null) return null;

        Node startNode = GridManager.Instance.NodeFromWorldPoint(startPos);
        Node targetNode = GridManager.Instance.NodeFromWorldPoint(targetPos);

        if (!startNode.walkable || !targetNode.walkable)
        {
            // Nếu đích đến là tường, tìm node đi được gần đích nhất
            if (!targetNode.walkable)
            {
                targetNode = FindNearestWalkableNode(targetNode);
                if (targetNode == null) return null;
            }
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        int maxIterations = 500; // Giới hạn số bước tìm kiếm để tránh đứng máy (lag)
        int iterations = 0;
        Node closestNode = startNode;
        int minHCost = int.MaxValue;

        while (openSet.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                // Vượt quá thời gian xử lý cho phép -> Trả về đường đi tạm thời đến node gần đích nhất đã tìm được
                return RetracePath(startNode, closestNode);
            }

            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode.hCost < minHCost)
            {
                minHCost = currentNode.hCost;
                closestNode = currentNode;
            }

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in GridManager.Instance.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;


                int penalty = 0;
                if (threatPos.HasValue)
                {
                    float distToThreat = Vector2.Distance(neighbor.worldPosition, threatPos.Value);
                    if (distToThreat < 8f)
                    {
                        // Phạt rất nặng nếu nằm gần địch
                        penalty += Mathf.RoundToInt((8f - distToThreat) * 50);
                        // Phạt thêm nếu nằm trong tầm nhìn thẳng của địch
                        if (CheckLineOfSight(neighbor.worldPosition, threatPos.Value))
                            penalty += 500;
                    }
                }
                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor) + penalty;
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        // Nếu không tìm được đường tới đích (hoặc đường cụt), đi tới điểm gần đích nhất
        if (closestNode != startNode)
        {
            return RetracePath(startNode, closestNode);
        }

        return null;
    }

    private Node FindNearestWalkableNode(Node targetNode)
    {
        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();
        
        queue.Enqueue(targetNode);
        visited.Add(targetNode);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            if (current.walkable) return current;

            foreach (Node neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return null;
    }

    private List<Vector2> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Add(startNode);
        
        List<Vector2> waypoints = SimplifyPath(path);
        waypoints.Reverse();
        return waypoints;
    }

    private List<Vector2> SimplifyPath(List<Node> path)
    {
        List<Vector2> rawWaypoints = new List<Vector2>();
        foreach (var node in path) rawWaypoints.Add(node.worldPosition);
        
        List<Vector2> waypoints = StringPulling(rawWaypoints);
        return ExtractStages(waypoints, 20);
    }

    private List<Vector2> ExtractStages(List<Vector2> waypoints, int maxStages)
    {
        if (waypoints == null || waypoints.Count <= 2) return waypoints;
        if (waypoints.Count <= maxStages) return waypoints;

        List<Vector2> result = new List<Vector2>();
        result.Add(waypoints[0]);

        // Tính toán bước nhảy index để rút gọn mảng xuống còn maxStages
        float step = (float)(waypoints.Count - 2) / (maxStages - 2);
        
        for (int i = 1; i < maxStages - 1; i++)
        {
            int idx = Mathf.RoundToInt(i * step);
            // Đảm bảo không trùng lặp và không vượt quá
            if (idx > 0 && idx < waypoints.Count - 1 && !result.Contains(waypoints[idx]))
            {
                result.Add(waypoints[idx]);
            }
        }

        result.Add(waypoints[waypoints.Count - 1]);
        return result;
    }

    private List<Vector2> StringPulling(List<Vector2> rawWaypoints)
    {
        if (rawWaypoints.Count <= 2) return rawWaypoints;

        List<Vector2> waypoints = new List<Vector2>();
        waypoints.Add(rawWaypoints[0]);

        int currentIdx = 0;
        while (currentIdx < rawWaypoints.Count - 1)
        {
            int furthestIdx = currentIdx + 1;
            for (int i = rawWaypoints.Count - 1; i > currentIdx + 1; i--)
            {
                if (CheckLineOfSight(rawWaypoints[currentIdx], rawWaypoints[i]))
                {
                    furthestIdx = i;
                    break;
                }
            }
            waypoints.Add(rawWaypoints[furthestIdx]);
            currentIdx = furthestIdx;
        }

        return waypoints;
    }

    private bool CheckLineOfSight(Vector2 start, Vector2 end)
    {
        Vector2 dir = (end - start);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;
        
        // Quét bán kính 0.45f để đảm bảo xe tăng (rộng ~0.9m) có thể lọt qua khe
        LayerMask mask = GridManager.Instance != null ? GridManager.Instance.unwalkableMask : layerVatCan;
        RaycastHit2D hit = Physics2D.CircleCast(start, 0.45f, dir.normalized, dist, mask);
        return hit.collider == null;
    }

    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    // ── 1. HỆ THỐNG TÌM CHỖ NẤP AN TOÀN (Risk Heatmap / Safe Retreat) ──
    public Vector2 FindSafeRetreatNode(Vector2 botPos, Vector2 enemyPos)
    {
        if (GridManager.Instance == null) return botPos;
        Node startNode = GridManager.Instance.NodeFromWorldPoint(botPos);

        Vector2 bestPos = botPos;
        float bestScore = -float.MaxValue;

        // Quét các Node trong bán kính nhỏ (tránh việc quét toàn map gây lag)
        int scanRadius = 15;
        for (int x = -scanRadius; x <= scanRadius; x++)
        {
            for (int y = -scanRadius; y <= scanRadius; y++)
            {
                int checkX = startNode.gridX + x;
                int checkY = startNode.gridY + y;

                if (checkX >= 0 && checkX < GridManager.Instance.gridSizeX && checkY >= 0 && checkY < GridManager.Instance.gridSizeY)
                {
                    Node node = GridManager.Instance.grid[checkX, checkY];
                    if (node.walkable)
                    {
                        float distToEnemy = Vector2.Distance(node.worldPosition, enemyPos);
                        float distToBot = Vector2.Distance(node.worldPosition, botPos);
                        
                        // Chấm điểm: Càng xa địch càng tốt, không nên đi quá xa điểm hiện tại (tránh chọn điểm ở bên kia tường vô lý)
                        float score = distToEnemy * 10f - distToBot * 2f;

                        // Ưu tiên số 1: Node khuất tầm nhìn (nằm sau tường/đá)
                        if (!CheckLineOfSight(node.worldPosition, enemyPos))
                        {
                            score += 1000f; // Điểm thưởng khổng lồ cho việc núp an toàn
                        }

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPos = node.worldPosition;
                        }
                    }
                }
            }
        }
        return bestPos;
    }
}
