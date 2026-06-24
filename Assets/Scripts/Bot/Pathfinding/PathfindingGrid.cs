using System.Collections.Generic;
using UnityEngine;

public class PathfindingGrid : MonoBehaviour
{
    public static PathfindingGrid Instance { get; private set; }

    [Header("Grid Settings")]
    public Vector2 GridWorldSize = new Vector2(160, 160);
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

    private void Start()
    {
        // Thay vi quet toan bo trong Update, ta dung Coroutine de chia nho khoi luong cong viec ra nhieu frame
        StartCoroutine(UpdateGridRoutine());
    }

    private System.Collections.IEnumerator UpdateGridRoutine()
    {
        while (true)
        {
            int nodesProcessed = 0;
            int maxNodesPerFrame = 1000; // Chi quet toi da 1000 o moi frame de khong gay giat lag

            for (int x = 0; x < _gridSizeX; x++)
            {
                for (int y = 0; y < _gridSizeY; y++)
                {
                    UpdateNodeWalkability(x, y);

                    nodesProcessed++;
                    if (nodesProcessed >= maxNodesPerFrame)
                    {
                        nodesProcessed = 0;
                        yield return null; // Tam nghi, cho den frame tiep theo moi quet tiep
                    }
                }
            }

            // Sau khi quet xong toan bo ban do, nghi 0.5 giay truoc khi bat dau chu ky quet moi
            yield return new WaitForSeconds(0.5f);
        }
    }

    private Collider2D[] _results = new Collider2D[10];

    public void UpdateGridWalkability()
    {
        if (_grid == null) return;
        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int y = 0; y < _gridSizeY; y++)
            {
                UpdateNodeWalkability(x, y);
            }
        }
    }

    private void UpdateNodeWalkability(int x, int y)
    {
        bool walkable = true;
        int penalty = 0;
        
        // Su dung NonAlloc de tranh tao ra rac bo nho (Garbage Collection) giup chong lag
        int hitCount = Physics2D.OverlapCircleNonAlloc(_grid[x, y].WorldPosition, NodeRadius * 1.0f, _results);
        for (int i = 0; i < hitCount; i++)
        {
            if (BotSteering.LaTuong(_results[i]))
            {
                walkable = false;
                break;
            }
        }

        if (walkable)
        {
            int penaltyHitCount = Physics2D.OverlapCircleNonAlloc(_grid[x, y].WorldPosition, NodeRadius * 2.5f, _results);
            for (int i = 0; i < penaltyHitCount; i++)
            {
                if (BotSteering.LaTuong(_results[i]))
                {
                    penalty = 25;
                    break;
                }
            }
        }

        _grid[x, y].Walkable = walkable;
        _grid[x, y].Penalty = penalty;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGridInScene()
    {
        if (Object.FindAnyObjectByType<PathfindingGrid>() != null) return;

        var go = new GameObject("PathfindingManager");
        go.AddComponent<PathfindingGrid>();
    }
}
