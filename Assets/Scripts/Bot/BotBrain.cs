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
        // --- ĐÓNG BĂNG GIAO TRANH VÀ RÚT LUI THEO YÊU CẦU ---
        // float scoreRutLui = TichDiemRutLui();
        // float scoreGiaoTranh = TichDiemGiaoTranh();
        float scoreNhatItem = TichDiemNhatItem();
        float scoreNhatCoin = TichDiemNhatCoin();

        // Mặc định là tuần tra (điểm số thấp nhất)
        IBotState muon = stateTuanTra;
        float maxScore = 0.1f;

        // if (scoreRutLui > maxScore)
        // {
        //     maxScore = scoreRutLui;
        //     muon = stateRutLui;
        // }
        // if (scoreGiaoTranh > maxScore)
        // {
        //     maxScore = scoreGiaoTranh;
        //     muon = stateGiaoTranh;
        // }
        if (scoreNhatItem > maxScore)
        {
            maxScore = scoreNhatItem;
            // TargetPos ở trong stateNhatCoin sẽ tự ưu tiên Item nếu có
            muon = stateNhatCoin; 
        }
        if (scoreNhatCoin > maxScore)
        {
            maxScore = scoreNhatCoin;
            muon = stateNhatCoin;
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

    private float TichDiemRutLui()
    {
        float nguongRutLui = CurrentConfig != null ? CurrentConfig.NguongMauRutLui : 0.3f;
        
        // CƠ CHẾ BÁM TRẠNG THÁI (Hysteresis):
        // Nếu đang trong trạng thái Rút lui (hồi máu), phải giữ trạng thái này cho tới khi máu đủ an toàn (80%).
        // Không để vàng hay item cám dỗ giữa chừng.
        if (currentState == stateRutLui)
        {
            if (ctx.HealthRatio < 0.8f)
            {
                // Ép điểm cực cao để đè bẹp mọi state khác (nhặt item = 60, vàng = 70)
                return 100f; 
            }
            // Đã hồi đủ 80%, cho phép làm việc khác
            return 0f;
        }

        // Nếu bình thường máu tụt qua ngưỡng nguy hiểm thì bắt đầu kích hoạt
        if (ctx.HealthRatio < nguongRutLui)
        {
            // Điểm từ 80 - 100 tùy độ yếu máu
            return 80f + 20f * (1f - ctx.HealthRatio);
        }
        return 0f;
    }

    private float TichDiemGiaoTranh()
    {
        if (ctx.NearestEnemy == null || ctx.DistanceToEnemy >= banKinhGiaoTranh) return 0f;
        
        float score = 75f; // Giao tranh cơ bản

        // Nếu sắp hết đạn (ít hơn 20 coin), giảm mạnh điểm giao tranh để trốn/nhặt coin
        if (!ctx.DuCoinDeBan(20)) score -= 40f; 

        return score;
    }

    private float TichDiemNhatItem()
    {
        if (ctx.NearestItem == null) return 0f;

        if (ignoredItems.Contains(ctx.NearestItem)) return 0f;

        if (!acceptedItems.Contains(ctx.NearestItem))
        {
            if (ctx.NearestItem.CanBePickedUpByBot(ctx, xacSuatNhatItem))
            {
                acceptedItems.Add(ctx.NearestItem);
            }
            else
            {
                ignoredItems.Add(ctx.NearestItem);
                return 0f;
            }
        }

        float score = 65f; // Điểm nhặt Item cơ bản (cao hơn Vàng 60f)

        bool hasEnemy = ctx.NearestEnemy != null && ctx.DistanceToEnemy < 15f;

        if (hasEnemy)
        {
            // Tình huống 4, 6: Có địch và Item
            float enemyDistToItem = Vector2.Distance(ctx.NearestEnemy.transform.position, ctx.NearestItem.transform.position);
            
            // Nếu kẻ địch ở gần Item hơn Bot -> Địch đã chiếm đóng Item
            if (enemyDistToItem < ctx.DistanceToItem && enemyDistToItem < 5f)
            {
                // Cực kỳ nguy hiểm -> Trừ sạch điểm để chê Item này
                score = -500f; 
            }
            else
            {
                // Item an toàn -> Thích nhặt (vì không có Giao tranh, ta thoải mái đi nhặt)
                score = 85f; 
            }
        }
        else
        {
            // Tình huống 2, 3: Không có địch -> Tăng nhẹ tùy số lượng
            score += ctx.ItemCountNearby;
        }

        return score;
    }

    private float TichDiemNhatCoin()
    {
        if (ctx.NearestCoin == null) return 0f;

        float score = 60f; // Điểm nhặt vàng cơ bản
        bool hasEnemy = ctx.NearestEnemy != null && ctx.DistanceToEnemy < 15f;

        // Tình huống 5, 6: Có địch
        if (hasEnemy)
        {
            float enemyDistToCoin = Vector2.Distance(ctx.NearestEnemy.transform.position, ctx.NearestCoin.transform.position);
            
            // Nếu kẻ địch đe doạ trực tiếp bãi vàng
            if (enemyDistToCoin < ctx.DistanceToCoin && enemyDistToCoin < 5f)
            {
                if (!ctx.DuCoinDeBan(20))
                {
                    // Ngoại lệ Tình huống 5: ĐANG HẾT ĐẠN -> LIỀU MẠNG XÔNG VÀO!
                    return 1000f; 
                }
                else
                {
                    // Đang dư tiền -> Địch giữ thì chê
                    return -500f;
                }
            }
        }

        // Tình huống Ưu tiên Tuyệt Đối: Hết đạn thì thèm vàng
        if (!ctx.DuCoinDeBan(20))
        {
            score += 20f; // Buff điểm Vàng (80f) vượt qua cả Item (65f)
        }

        // Tình huống 7: Rất nhiều vàng (>=5) nhưng ít item (<=1)
        if (ctx.CoinCountNearby >= 5 && ctx.ItemCountNearby <= 1)
        {
            score += 3f * ctx.CoinCountNearby; // Điểm vọt lên rất cao (> 75đ), lóa mắt vì mỏ vàng
        }
        else
        {
            // Tình huống 1: Chỉ có vàng -> Nhặt tà tà
            score += ctx.CoinCountNearby;
        }

        // Tình huống 3: Có vàng và item (Bình thường Item=65 > Vàng=60)
        // Nhưng nếu Vàng nằm ngay sát chân (<3m), tạo 20% khả năng Bot sẽ tham lam chộp luôn cục Vàng đó trước.
        if (ctx.ItemCountNearby > 0 && ctx.DistanceToCoin < 3f)
        {
            if (ctx.NearestCoin.gameObject.GetInstanceID() % 5 == 0)
            {
                score = 70f; // Vượt mốc 65 của Item
            }
        }

        return score;
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
