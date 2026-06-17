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

        bool targetWasWalkable = targetNode.walkable;

        if (!startNode.walkable || !targetWasWalkable)
        {
            if (!startNode.walkable)
            {
                startNode = FindNearestWalkableNode(startNode);
                if (startNode == null) return null;
            }
            
            if (!targetWasWalkable)
            {
                targetNode = FindNearestWalkableNode(targetNode);
                if (targetNode == null) return null;
            }
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        int maxIterations = 5000; // [FIXED] Tăng giới hạn lên 5000 để đủ sức tìm đường vòng qua các chướng ngại vật lớn
        int iterations = 0;
        Node closestNode = startNode;
        int minHCost = int.MaxValue;

        while (openSet.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                // Vượt quá thời gian xử lý cho phép -> Trả về đường đi tạm thời đến node gần đích nhất đã tìm được
                return RetracePath(startNode, closestNode, null);
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
                return RetracePath(startNode, targetNode, targetPos);
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
                        if (Vector2.Distance(neighbor.worldPosition, threatPos.Value) < 15f)
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
            return RetracePath(startNode, closestNode, targetPos);
        }

        return null;
    }

    public Node FindNearestWalkableNode(Node targetNode)
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

    private List<Vector2> RetracePath(Node startNode, Node endNode, Vector2? exactEndPos)
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
        
        // [FIXED] Ghi đè trạm cuối cùng bằng tọa độ chính xác của mục tiêu (thay vì tâm của ô Grid).
        // Nếu không, AI sẽ đi đến giữa ô rồi dừng lại, không chạm được vào Item nằm ở mép ô!
        if (waypoints.Count > 0 && exactEndPos.HasValue)
        {
            waypoints[waypoints.Count - 1] = exactEndPos.Value;
        }
        
        return waypoints;
    }

    private List<Vector2> SimplifyPath(List<Node> path)
    {
        List<Vector2> rawWaypoints = new List<Vector2>();
        foreach (var node in path) rawWaypoints.Add(node.worldPosition);
        
        // Bật lại StringPulling để làm mượt đường đi. Điều này giúp các xe từ các vị trí khác nhau
        // sẽ có góc độ đường đi khác nhau (đường chéo) thay vì tất cả cùng đi chung 1 đường kẻ caro vuông vức!
        return StringPulling(rawWaypoints);
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
            
            // Tối ưu hóa cực mạnh: Duyệt XUÔI (O(N)) thay vì duyệt NGƯỢC (O(N^2)). 
            for (int i = currentIdx + 2; i < rawWaypoints.Count; i++)
            {
                // Bán kính 0.65f lớn hơn thân xe một chút. Nó buộc A* không được xóa các waypoint ở góc cua hẹp, giúp xe rẽ theo từng giai đoạn an toàn.
                if (CheckLineOfSight(rawWaypoints[currentIdx], rawWaypoints[i], 0.65f))
                {
                    furthestIdx = i; // Còn nhìn thấy thì cứ đẩy điểm xa nhất lên
                }
                else
                {
                    break; // Ngay khi bị tường che, dừng lại và chốt trạm ở điểm nhìn thấy cuối cùng
                }
            }
            
            waypoints.Add(rawWaypoints[furthestIdx]);
            currentIdx = furthestIdx;
        }

        return waypoints;
    }

    public bool CheckLineOfSight(Vector2 start, Vector2 end, float radius = 0.45f)
    {
        if (GridManager.Instance == null) return false;

        // [BẢN VÁ TUYỆT ĐỐI] Không dùng Physics2D nữa vì dễ bị lọt qua các khe hở collider hoặc sai layer!
        // Dùng thuật toán dò tia trực tiếp trên Ma Trận Grid (Bresenham's Line Algorithm)
        Node startNode = GridManager.Instance.NodeFromWorldPoint(start);
        Node endNode = GridManager.Instance.NodeFromWorldPoint(end);

        int x0 = startNode.gridX;
        int y0 = startNode.gridY;
        int x1 = endNode.gridX;
        int y1 = endNode.gridY;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < GridManager.Instance.gridSizeX && y0 >= 0 && y0 < GridManager.Instance.gridSizeY)
            {
                Node n = GridManager.Instance.grid[x0, y0];
                if (!n.walkable) return false; // Tia chạm trúng 1 ô bị kẹt (Tường, Đá)
                
                // Nếu có yêu cầu bán kính (radius > 0), kiểm tra thêm các ô xung quanh để tránh quẹt góc tường
                if (radius > 0)
                {
                    if (x0 + 1 < GridManager.Instance.gridSizeX && !GridManager.Instance.grid[x0 + 1, y0].walkable) return false;
                    if (x0 - 1 >= 0 && !GridManager.Instance.grid[x0 - 1, y0].walkable) return false;
                    if (y0 + 1 < GridManager.Instance.gridSizeY && !GridManager.Instance.grid[x0, y0 + 1].walkable) return false;
                    if (y0 - 1 >= 0 && !GridManager.Instance.grid[x0, y0 - 1].walkable) return false;
                }
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        
        return true;
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
