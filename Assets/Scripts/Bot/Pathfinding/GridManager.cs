using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GridManager : NetworkBehaviour
{
    public static GridManager Instance { get; private set; }

    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize = new Vector2(40, 40);
    public float nodeRadius = 0.5f;
    public Vector2 gridCenter = Vector2.zero;

    public Node[,] grid;
    public List<Node> walkablePlayableNodes = new List<Node>();
    private float nodeDiameter;
    public int gridSizeX, gridSizeY;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }

    public int MaxSize => gridSizeX * gridSizeY;

    public void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector2 worldBottomLeft = gridCenter - Vector2.right * gridWorldSize.x / 2 - Vector2.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2 worldPoint = worldBottomLeft + Vector2.right * (x * nodeDiameter + nodeRadius) + Vector2.up * (y * nodeDiameter + nodeRadius);
                bool walkable = !Physics2D.OverlapCircle(worldPoint, 0.85f, unwalkableMask);
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
        
        CalculatePlayableArea();
    }
    
    public void CalculatePlayableArea()
    {
        walkablePlayableNodes.Clear();
        Node startNode = NodeFromWorldPoint(gridCenter);
        
        // Tìm 1 điểm ở giữa bản đồ làm tâm quét (đề phòng tâm chính xác lại nằm đè lên cục đá)
        int searchRadius = 1;
        while (!startNode.walkable && searchRadius < 20)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * searchRadius;
            startNode = NodeFromWorldPoint(gridCenter + offset);
            searchRadius++;
        }
        
        if (!startNode.walkable) return;

        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            walkablePlayableNodes.Add(current);

            foreach (Node neighbor in GetNeighbors(current))
            {
                if (neighbor.walkable && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    // Chống đi xuyên góc chéo (Corner Clipping)
                    if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                    {
                        bool walkX = grid[checkX, node.gridY].walkable;
                        bool walkY = grid[node.gridX, checkY].walkable;
                        if (!walkX || !walkY) continue; // Phải hở cả 2 bên mới được đi chéo
                    }
                    neighbors.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbors;
    }

    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        // Calculate percentages
        float percentX = (worldPosition.x - gridCenter.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.y - gridCenter.y + gridWorldSize.y / 2) / gridWorldSize.y;
        
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        
        return grid[x, y];
    }

    public void UpdateNodeAtPosition(Vector2 worldPosition)
    {
        Node node = NodeFromWorldPoint(worldPosition);
        if (node != null)
        {
            bool walkable = !Physics2D.OverlapCircle(node.worldPosition, 0.85f, unwalkableMask);
            node.walkable = walkable;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(gridCenter, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));
        if (grid != null)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = n.walkable ? new Color(1, 1, 1, 0.3f) : new Color(1, 0, 0, 0.3f);
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }
}
