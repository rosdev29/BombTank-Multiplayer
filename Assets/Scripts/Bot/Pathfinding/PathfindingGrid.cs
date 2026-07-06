using System.Collections.Generic;
using UnityEngine;

public class PathfindingGrid : MonoBehaviour
{
    public static PathfindingGrid Instance { get; private set; }

    [Header("Grid Settings")]
    public Vector2 GridWorldSize = new Vector2(160, 160);
    public float NodeRadius = 1.0f;

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

        // Tự căn chỉnh grid về đúng tâm map trước khi tạo lưới
        CenterGridOnMap();
        CreateGrid();
    }

    private float _updateTimer;

    /// <summary>
    /// Tự động tính tâm của tất cả Collider2D tường trong scene,
    /// rồi đặt transform.position của grid về đúng tâm đó.
    /// Nhờ vậy grid luôn bao phủ đúng khu vực map dù map không ở (0,0).
    /// </summary>
    private void CenterGridOnMap()
    {
        // Quét tất cả Collider2D trong scene (non-trigger)
        Collider2D[] allColliders = Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        foreach (Collider2D col in allColliders)
        {
            if (!BotSteering.LaTuong(col)) continue;

            if (!found)
            {
                combinedBounds = col.bounds;
                found = true;
            }
            else
            {
                combinedBounds.Encapsulate(col.bounds);
            }
        }

        if (found)
        {
            // Đặt tâm grid đúng vào tâm của toàn bộ tường
            Vector2 mapCenter = combinedBounds.center;
            transform.position = new Vector3(mapCenter.x, mapCenter.y, 0f);
            Debug.Log($"[PathfindingGrid] Tâm map tự động: {mapCenter} " +
                      $"(bounds size: {combinedBounds.size.x:F1}x{combinedBounds.size.y:F1})");
        }
        else
        {
            Debug.LogWarning("[PathfindingGrid] Không tìm thấy Collider tường nào! Grid đặt tại (0,0).");
        }
    }

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

    private Collider2D[] _results = new Collider2D[16];

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

    // Ngưỡng penalty theo khoảng cách tới tường (tính theo bội số NodeRadius)
    private const float PENALTY_RADIUS_NEAR   = 1.5f;  // Penalty 80 — sát tường, cực kỳ tránh
    private const float PENALTY_RADIUS_MID    = 2.5f;  // Penalty 35 — gần tường
    private const float PENALTY_RADIUS_FAR    = 3.5f;  // Penalty 10 — hơi xa tường

    private void UpdateNodeWalkability(int x, int y)
    {
        Vector2 pos      = _grid[x, y].WorldPosition;
        bool    walkable = true;
        int     penalty  = 0;

        // Dùng NonAlloc để tránh tạo ra rác bộ nhớ (GC).
        // Bán kính 0.45× NodeRadius: chỉ mark non-walkable khi tâm ô thực sự nằm trong/sát vật thể,
        // tránh vùng đỏ tràn ra ngoài hình học thực tế của tường.
        int hitCount = Physics2D.OverlapCircleNonAlloc(pos, NodeRadius * 0.45f, _results);
        for (int i = 0; i < hitCount; i++)
        {
            if (BotSteering.LaTuong(_results[i]))
            {
                walkable = false;
                break;
            }
        }

        // Penalty gradient 3 mức: càng gần tường càng bị phạt nặng
        // A* sẽ ưu tiên đường đi qua giữa hành lang thay vì đi sát tường
        if (walkable)
        {
            // Mức 1: Sát tường → penalty rất cao
            int nearCount = Physics2D.OverlapCircleNonAlloc(pos, NodeRadius * PENALTY_RADIUS_NEAR, _results);
            for (int i = 0; i < nearCount; i++)
            {
                if (BotSteering.LaTuong(_results[i]))
                {
                    penalty = 80;
                    break;
                }
            }

            // Mức 2: Gần tường
            if (penalty == 0)
            {
                int midCount = Physics2D.OverlapCircleNonAlloc(pos, NodeRadius * PENALTY_RADIUS_MID, _results);
                for (int i = 0; i < midCount; i++)
                {
                    if (BotSteering.LaTuong(_results[i]))
                    {
                        penalty = 35;
                        break;
                    }
                }
            }

            // Mức 3: Hơi xa tường
            if (penalty == 0)
            {
                int farCount = Physics2D.OverlapCircleNonAlloc(pos, NodeRadius * PENALTY_RADIUS_FAR, _results);
                for (int i = 0; i < farCount; i++)
                {
                    if (BotSteering.LaTuong(_results[i]))
                    {
                        penalty = 10;
                        break;
                    }
                }
            }
        }

        _grid[x, y].Walkable = walkable;
        _grid[x, y].Penalty  = penalty;
    }

    public Node NodeFromWorldPoint(Vector2 worldPosition)
    {
        // worldBottomLeft là góc dưới-trái của grid (tâm ô [0,0] = worldBottomLeft + NodeRadius)
        Vector2 worldBottomLeft = (Vector2)transform.position
            - Vector2.right * GridWorldSize.x / 2
            - Vector2.up    * GridWorldSize.y / 2;

        // Tính chỉ số ô bằng cách lấy khoảng cách từ worldBottomLeft rồi chia cho đường kính ô
        // FloorToInt để map đúng: mọi điểm trong ô [x,y] đều trả về [x,y]
        int x = Mathf.FloorToInt((worldPosition.x - worldBottomLeft.x) / _nodeDiameter);
        int y = Mathf.FloorToInt((worldPosition.y - worldBottomLeft.y) / _nodeDiameter);

        x = Mathf.Clamp(x, 0, _gridSizeX - 1);
        y = Mathf.Clamp(y, 0, _gridSizeY - 1);

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
    // Màu: đỏ = không đi được | vàng/cam/xanh nhạt = penalty gần tường | trắng mờ = thoáng
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(GridWorldSize.x, GridWorldSize.y, 1));

        if (_grid != null)
        {
            float drawSize = _nodeDiameter - 0.05f; // Viền mỏng để thấy lưới
            foreach (Node n in _grid)
            {
                if (!n.Walkable)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.6f);       // Đỏ: tường / non-walkable
                }
                else if (n.Penalty >= 80)
                {
                    Gizmos.color = new Color(1f, 0.3f, 0f, 0.45f);    // Cam đậm: sát tường
                }
                else if (n.Penalty >= 35)
                {
                    Gizmos.color = new Color(1f, 0.8f, 0f, 0.35f);    // Vàng: gần tường
                }
                else if (n.Penalty >= 10)
                {
                    Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.25f);  // Xanh nhạt: hơi xa tường
                }
                else
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.08f);      // Trắng mờ: thoáng
                }
                Gizmos.DrawCube(n.WorldPosition, Vector3.one * drawSize);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGridInScene()
    {
        if (Object.FindAnyObjectByType<PathfindingGrid>() != null) return;

        var go = new GameObject("PathfindingManager");
        go.transform.position = Vector3.zero; // Tạm đặt (0,0); CenterGridOnMap() trong Awake sẽ tự sửa
        go.AddComponent<PathfindingGrid>();
    }
}
