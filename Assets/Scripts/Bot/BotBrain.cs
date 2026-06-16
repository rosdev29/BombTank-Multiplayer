using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TankPlayer))]
[RequireComponent(typeof(BotSense))]
public class BotBrain : NetworkBehaviour
{
    [Header("Chu kỳ đánh giá")]
    [Tooltip("Bot đánh giá lại trạng thái sau mỗi X giây (random trong khoảng min-max)")]
    [SerializeField] private float chuKyDanhGiaMin = 0.2f;
    [SerializeField] private float chuKyDanhGiaMax = 0.5f;

    [Header("Ngưỡng chuyển trạng thái")]
    [SerializeField] private float banKinhGiaoTranh      = 10f;
    [SerializeField] private float xacSuatNhatItem       = 0.5f;

    [Header("Độ Khó (Cấu hình)")]
    [SerializeField] private BotConfig[] botConfigs;
    public BotConfig CurrentConfig { get; private set; }

    [Header("Cảm biến tránh vật cản")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float rayDistance = 3f;
    [SerializeField] private float rayAngle = 30f;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    public static IReadOnlyList<TankPlayer> AllPlayers => _allPlayers;
    private static readonly List<TankPlayer> _allPlayers = new List<TankPlayer>();

    private BotContext  ctx;
    private BotSense    sense;
    private TankPlayer  tankPlayer;
    private Rigidbody2D rb;

    private IBotState stateTuanTra;
    private IBotState stateGiaoTranh;
    private IBotState stateNhatCoin;
    private IBotState stateRutLui;
    private IBotState currentState;

    private float _timerDanhGia;
    
    private HashSet<ItemPickup> ignoredItems = new HashSet<ItemPickup>();
    private HashSet<ItemPickup> acceptedItems = new HashSet<ItemPickup>();

    private BotCommand currentCommand;
    private List<Vector2> currentPath;

    private void Awake()
    {
        tankPlayer = GetComponent<TankPlayer>();
        sense      = GetComponent<BotSense>();
        rb         = GetComponent<Rigidbody2D>();

        stateTuanTra   = new TrangThaiTuanTra();
        stateGiaoTranh = new TrangThaiGiaoTranh();
        stateNhatCoin  = new TrangThaiNhatCoin();
        stateRutLui    = new TrangThaiRutLui();

        if (obstacleLayer.value == 0)
        {
            // Mặc định dò mọi thứ ngoại trừ Player và các lớp không cần thiết
            obstacleLayer = ~LayerMask.GetMask("Player", "Ignore Raycast");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }

        TankPlayer.OnPlayerSpawned   += OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned += OnPlayerDespawned;

        if (botConfigs != null && botConfigs.Length > 0)
        {
            CurrentConfig = botConfigs[Random.Range(0, botConfigs.Length)];
        }
        else
        {
            CurrentConfig = ScriptableObject.CreateInstance<BotConfig>();
        }

        ctx = new BotContext
        {
            Player        = tankPlayer,
            BodyTransform = transform,
            Health        = tankPlayer.Health,
            Wallet        = tankPlayer.Wallet,
            Config        = CurrentConfig,
        };

        _timerDanhGia = RandomChuKy();
        ChuyenTrangThai(stateTuanTra);
    }

    public override void OnNetworkDespawn()
    {
        TankPlayer.OnPlayerSpawned   -= OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned -= OnPlayerDespawned;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || ctx == null) { return; }

        if (_timerDanhGia <= 0f)
        {
            _timerDanhGia = RandomChuKy();
            ctx.DeltaTime = Time.deltaTime;

            ignoredItems.RemoveWhere(i => i == null || !i.isActiveAndEnabled);
            acceptedItems.RemoveWhere(i => i == null || !i.isActiveAndEnabled);

            sense.DocMoiTruong(ctx);
            ChonTrangThai();

            currentCommand = currentState.Update(ctx);

            if (currentCommand.PathDestination.HasValue)
            {
                if (AStarPathfinding.Instance != null)
                {
                    currentPath = AStarPathfinding.Instance.FindPath(ctx.BotPosition, currentCommand.PathDestination.Value);
                }
                else
                {
                    // Fallback nếu chưa có lưới
                    currentPath = new List<Vector2> { currentCommand.PathDestination.Value };
                }
            }
            else
            {
                currentPath = null;
            }
        }

        if (currentCommand != null)
        {
            ctx.OutputHuongDiChuyen = currentCommand.MoveInput;
            ctx.OutputDiemNgam      = currentCommand.AimTarget ?? ctx.BotPosition;
            ctx.OutputCoBopCo       = currentCommand.Fire;

            ThucThiLenhTheoPath();
        }

        CapNhatLabelDebug();
    }

    private void ChonTrangThai()
    {
        IBotState muon;

        float nguongRutLui = CurrentConfig != null ? CurrentConfig.NguongMauRutLui : 0.3f;

        if (ctx.HealthRatio < nguongRutLui)
        {
            muon = stateRutLui;
        }
        else if (ctx.NearestEnemy != null && ctx.DistanceToEnemy < banKinhGiaoTranh)
        {
            muon = stateGiaoTranh;
        }
        else if (ctx.NearestItem != null)
        {
            if (!ignoredItems.Contains(ctx.NearestItem) && !acceptedItems.Contains(ctx.NearestItem))
            {
                // Gọi sang ItemPickup để item tự quyết định
                if (ctx.NearestItem.CanBePickedUpByBot(ctx, xacSuatNhatItem))
                    acceptedItems.Add(ctx.NearestItem);
                else
                    ignoredItems.Add(ctx.NearestItem);
            }

            if (acceptedItems.Contains(ctx.NearestItem))
                muon = stateNhatCoin;
            else
                muon = stateTuanTra;
        }
        else if (ctx.NearestCoin != null)
        {
            muon = stateNhatCoin;
        }
        else
        {
            muon = stateTuanTra;
        }

        if (muon != currentState)
        {
            ChuyenTrangThai(muon);
        }

        // Tự động dùng item nếu đang có item lợi và đang giao tranh
        if (currentState == stateGiaoTranh && tankPlayer.Inventory != null && tankPlayer.Inventory.CurrentItem.Value != ItemType.None)
        {
            if (tankPlayer.Inventory.CurrentItem.Value == ItemType.BuffCoin || tankPlayer.Inventory.CurrentItem.Value == ItemType.DoubleBarrel)
            {
                tankPlayer.Inventory.UseItem();
            }
        }
    }

    private void ChuyenTrangThai(IBotState tiepTheo)
    {
        currentState?.OnExit(ctx);
        currentState = tiepTheo;
        currentState.OnEnter(ctx);

        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} → {TenTrangThai(currentState)}");
    }

    private void ThucThiLenhTheoPath()
    {
        if (rb == null || currentCommand == null) { return; }

        float steer = currentCommand.MoveInput.x;
        float throttle = currentCommand.MoveInput.y;

        // Đi theo đường A* nếu có
        if (currentPath != null && currentPath.Count > 0)
        {
            Vector2 targetNode = currentPath[0];
            Vector2 huong = targetNode - ctx.BotPosition;
            
            // Xóa điểm nếu đã đến gần
            if (huong.magnitude < 1.0f)
            {
                currentPath.RemoveAt(0);
                if (currentPath.Count > 0)
                {
                    targetNode = currentPath[0];
                    huong = targetNode - ctx.BotPosition;
                }
            }

            if (currentPath.Count > 0)
            {
                float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);
                steer = gocLech > 0 ? -1f : 1f;
                throttle = 1f;

                // Nếu lệch quá nhiều thì đứng lại xoay cho chuẩn
                if (Mathf.Abs(gocLech) > 45f)
                {
                    throttle = 0.2f;
                }
            }
            else
            {
                throttle = 0f;
            }
        }

        float tocDo     = 5f;
        float tocDoXoay = 180f;

        TranhVatCan(ref steer, ref throttle);

        ctx.BodyTransform.Rotate(0f, 0f, steer * -tocDoXoay * Time.deltaTime);
        rb.velocity = (Vector2)ctx.BodyTransform.up * throttle * tocDo;
    }

    private void TranhVatCan(ref float steer, ref float throttle)
    {
        if (throttle <= 0.1f) return; // Chỉ né khi đang đi tới

        Vector2 origin = ctx.BodyTransform.position;
        Vector2 forward = ctx.BodyTransform.up;
        
        Vector2 leftDir = Quaternion.Euler(0, 0, rayAngle) * forward;
        Vector2 rightDir = Quaternion.Euler(0, 0, -rayAngle) * forward;

        RaycastHit2D hitCenter = Physics2D.Raycast(origin, forward, rayDistance, obstacleLayer);
        RaycastHit2D hitLeft = Physics2D.Raycast(origin, leftDir, rayDistance * 0.8f, obstacleLayer);
        RaycastHit2D hitRight = Physics2D.Raycast(origin, rightDir, rayDistance * 0.8f, obstacleLayer);

        if (hitCenter.collider != null)
        {
            // Nếu quá gần tường, cài số lùi
            if (hitCenter.distance < 1.0f)
            {
                throttle = -1f;
                steer = (Random.value > 0.5f) ? 1f : -1f; // Bẻ lái để thoát kẹt
                return;
            }

            // Chọn bên nào trống hơn để bẻ lái
            float leftDist = hitLeft.collider != null ? hitLeft.distance : rayDistance;
            float rightDist = hitRight.collider != null ? hitRight.distance : rayDistance;

            if (leftDist > rightDist)
            {
                steer = -1f; // Rẽ trái
            }
            else
            {
                steer = 1f;  // Rẽ phải
            }
        }
        else if (hitLeft.collider != null && hitLeft.distance < 1.5f)
        {
            steer = 1f; // Rẽ phải nhẹ
        }
        else if (hitRight.collider != null && hitRight.distance < 1.5f)
        {
            steer = -1f; // Rẽ trái nhẹ
        }
    }

    private void CapNhatLabelDebug()
    {
        if (labelTrangThai == null) { return; }
        labelTrangThai.text = TenTrangThai(currentState);
    }

    private static string TenTrangThai(IBotState s) => s switch
    {
        TrangThaiGiaoTranh => "⚔️ Giao tranh",
        TrangThaiNhatCoin  => "💰 Nhặt coin",
        TrangThaiRutLui    => "🏃 Rút lui",
        TrangThaiTuanTra   => "🔍 Tuần tra",
        _                  => s?.GetType().Name ?? "null"
    };

    private float RandomChuKy() => Random.Range(chuKyDanhGiaMin, chuKyDanhGiaMax);

    private static void OnPlayerSpawned(TankPlayer p)
    {
        if (!_allPlayers.Contains(p)) { _allPlayers.Add(p); }
    }

    private static void OnPlayerDespawned(TankPlayer p)
    {
        _allPlayers.Remove(p);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || ctx == null || ctx.BodyTransform == null) return;

        Vector2 origin = ctx.BodyTransform.position;
        Vector2 forward = ctx.BodyTransform.up;
        Vector2 leftDir = Quaternion.Euler(0, 0, rayAngle) * forward;
        Vector2 rightDir = Quaternion.Euler(0, 0, -rayAngle) * forward;

        DrawRayGizmo(origin, forward, rayDistance);
        DrawRayGizmo(origin, leftDir, rayDistance * 0.8f);
        DrawRayGizmo(origin, rightDir, rayDistance * 0.8f);

        // Vẽ đường A*
        if (currentPath != null)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
        }
    }

    private void DrawRayGizmo(Vector2 origin, Vector2 dir, float dist)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, obstacleLayer);
        if (hit.collider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, hit.point);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + dir * dist);
        }
    }
}
