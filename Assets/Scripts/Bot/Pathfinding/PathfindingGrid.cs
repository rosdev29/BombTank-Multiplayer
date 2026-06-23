using System.Collections.Generic;
using UnityEngine;

public class PathfindingGrid : MonoBehaviour
{
    public static PathfindingGrid Instance { get; private set; }

    [Header("Grid Settings")]
    public Vector2 GridWorldSize = new Vector2(60, 60);
    public float NodeRadius = 0.5f;

    private Node[,] _grid;
    private float _nodeDiameter;
    private int _gridSizeX;
    private int _gridSizeY;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _nodeDiameter = NodeRadius * 2;
        _gridSizeX = Mathf.RoundToInt(GridWorldSize.x / _nodeDiameter);
        _gridSizeY = Mathf.RoundToInt(GridWorldSize.y / _nodeDiameter);

        CreateGrid();
    }

    private float _updateTimer;

    private void CreateGrid()
    {
        _grid = new Node[_gridSizeX, _gridSizeY];
        Vector2 worldBottomLeft = (Vector2)transform.position - Vector2.right * GridWorldSize.x / 2 - Vector2.up * GridWorldSize.y / 2;

        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int y = 0; y < _gridSizeY; y++)
            {
                Vector2 worldPoint = worldBottomLeft + Vector2.right * (x * _nodeDiameter + NodeRadius) + Vector2.up * (y * _nodeDiameter + NodeRadius);
                _grid[x, y] = new Node(true, worldPoint, x, y);
            }
        }
        UpdateGridWalkability();
    }

    private void Update()
    {
        _updateTimer -= Time.deltaTime;
        if (_updateTimer <= 0f)
        {
            _updateTimer = 1.0f; // Quet lai grid moi 1 giay
            UpdateGridWalkability();
        }
    }

    public void UpdateGridWalkability()
    {
        if (_grid == null) return;
        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int y = 0; y < _gridSizeY; y++)
            {
                bool walkable = true;
                int penalty = 0;
                
                // Kiem tra vat can sat ranh gioi (Walkability)
                Collider2D[] cols = Physics2D.OverlapCircleAll(_grid[x, y].WorldPosition, NodeRadius * 1.0f);
                foreach (var col in cols)
                {
                    if (BotSteering.LaTuong(col))
                    {
                        walkable = false;
                        break;
                    }
                }

                if (walkable)
                {
                    // Neu di duoc, kiem tra xem co nam gan tuong khong de tang Penalty
                    // Ban kinh 2.5 lan NodeRadius de phat hien tuong o gan
                    Collider2D[] penaltyCols = Physics2D.OverlapCircleAll(_grid[x, y].WorldPosition, NodeRadius * 2.5f);
                    foreach (var col in penaltyCols)
                    {
                        if (BotSteering.LaTuong(col))
                        {
                            penalty = 25; // Phat them 25 diem chi phi neu di sat tuong (khuyen khich di ra giua duong)
                            break;
                        }
                    }
                }

                _grid[x, y].Walkable = walkable;
                _grid[x, y].Penalty = penalty;
            }
        }
    }

    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        float percentX = (worldPosition.x + GridWorldSize.x / 2) / GridWorldSize.x;
        float percentY = (worldPosition.y + GridWorldSize.y / 2) / GridWorldSize.y;
        
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((_gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((_gridSizeY - 1) * percentY);

        return _grid[x, y];
    }

    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.GridX + x;
                int checkY = node.GridY + y;

                if (checkX >= 0 && checkX < _gridSizeX && checkY >= 0 && checkY < _gridSizeY)
                {
                    // Chinh sua: Ngan chan loi "cat goc" (Corner cutting)
                    if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                    {
                        bool walk1 = _grid[node.GridX + x, node.GridY].Walkable;
                        bool walk2 = _grid[node.GridX, node.GridY + y].Walkable;
                        
                        // Neu 1 trong 2 o thang ben canh bi chan boi tuong, cam di cheo qua goc do
                        if (!walk1 || !walk2)
                        {
                            continue;
                        }
                    }

                    neighbors.Add(_grid[checkX, checkY]);
                }
            }
        }

        return neighbors;
    }

    // De visualize trong Editor
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(GridWorldSize.x, GridWorldSize.y, 1));

        if (_grid != null)
        {
            foreach (Node n in _grid)
            {
                Gizmos.color = n.Walkable ? new Color(1, 1, 1, 0.3f) : new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(n.WorldPosition, Vector3.one * (_nodeDiameter - 0.1f));
            }
        }
    }
}
