using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BotMemorySystem — Bộ nhớ bản đồ và hệ thống mục tiêu cho Bot.
///
/// CHỨC NĂNG:
///   [1] Bản đồ Đã Thăm (Visited Grid)
///       - Lưới ô 2m, đánh dấu mọi vị trí bot đã đi qua
///       - Dùng để "nhớ lại" vùng nào đã đi, vùng nào chưa
///
///   [2] Điểm Quan Tâm (POI — Points of Interest)
///       - Trạm hồi máu / Item / Coin: nhớ vị trí khi nhìn thấy
///       - Khi máu thấp → lục lại memory tìm đường đến trạm hồi máu
///
///   [3] Hệ thống Mục tiêu (Goal System)
///       - Bot LUÔN CÓ mục tiêu, không bao giờ đi lung tung
///       - Ưu tiên theo thứ tự: Hồi máu > Giao tranh > Nhặt đồ > Khám phá
///
///   [4] Anti-Looping
///       - Lưu 30 vị trí gần nhất
///       - Phát hiện bot đang quay vòng → đổi mục tiêu mới
///
/// RESET: Toàn bộ memory xóa khi bot chết (OnEpisodeBegin)
/// </summary>
public class BotMemorySystem : MonoBehaviour
{
    // ─── Cấu trúc POI ──────────────────────────────────────────
    public enum POIType { HealStation, Item, Coin, Enemy, Unknown }

    public struct MemoryPOI
    {
        public Vector2 worldPos;
        public POIType type;
        public float   timeAdded;    // Time.time lúc nhớ
        public float   importance;   // 0-1, fade theo thời gian

        public bool IsStillValid(float maxAge = 60f)
            => (Time.time - timeAdded) < maxAge;
    }

    // ─── Kiểu mục tiêu ─────────────────────────────────────────
    public enum GoalType { Heal, Combat, Loot, Explore, Retreat }

    // ─── Cấu hình ──────────────────────────────────────────────
    [Header("Cấu hình Bộ nhớ")]
    [SerializeField] public float cellSize         = 2f;   // Kích thước ô nhớ bản đồ
    [SerializeField] public int   maxPOI           = 20;   // Số POI tối đa mỗi loại
    [SerializeField] public float poiMaxAge        = 90f;  // Thời gian POI còn hiệu lực
    [SerializeField] public int   pathHistoryLen   = 8;   // Số bước nhớ để chống lặp

    [Header("Cấu hình Mục tiêu")]
    [SerializeField] public float healThreshold    = 0.35f;  // Dưới 35% → tìm trạm hồi máu
    [SerializeField] public float explorationRange = 20f;    // Tầm khám phá khi không có gì
    [SerializeField] public float goalReachedDist  = 0.6f;   // Khoảng cách "đã đến mục tiêu"
    [SerializeField] public float loopDetectRadius = 2f;     // Bán kính phát hiện lặp vòng
    [SerializeField] public int   loopDetectWindow = 4;      // Cửa sổ frames để phát hiện lặp

    // ─── DỮ LIỆU BỘ NHỚ ───────────────────────────────────────
    private HashSet<Vector2Int>   _visited     = new HashSet<Vector2Int>();
    private HashSet<Vector2Int>   _unreachableCells = new HashSet<Vector2Int>();

    [Header("Cấu hình nhặt đồ")]
    [Range(0f, 1f)] public float tiLeNhatItem = 0.5f;
    private HashSet<Vector2Int>   _ignoredItems = new HashSet<Vector2Int>();

    [SerializeField] private List<MemoryPOI>       _healPOIs    = new List<MemoryPOI>();
    [SerializeField] private List<MemoryPOI>       _itemPOIs    = new List<MemoryPOI>();
    [SerializeField] private List<MemoryPOI>       _coinPOIs    = new List<MemoryPOI>();
    [SerializeField] private List<MemoryPOI>       _enemyPOIs   = new List<MemoryPOI>();

    // Biến lưu vết kiểm tra kẹt
    private float _stuckTime = 0f;
    private Vector2 _lastStuckPos;

    // Lịch sử đường đi
    private Queue<Vector2>        _pathHistory = new Queue<Vector2>();

    // ─── HỆ THỐNG MỤC TIÊU ────────────────────────────────────
    public GoalType CurrentGoal       { get; private set; } = GoalType.Explore;
    public Vector2  GoalPosition      { get; private set; } = Vector2.zero;
    public Vector2  FinalGoalPosition { get; private set; } = Vector2.zero;
    public float    GoalUrgency       { get; private set; } = 0.5f;
    public bool     IsLooping         { get; private set; } = false;
    public float    CoverageRatio     { get; private set; } = 0f;

    // Danh sách các trạm trung chuyển do A* lập ra
    public List<Vector2> _pathWaypoints = new List<Vector2>();

    // Thống kê thăm dò bản đồ
    private const int MAP_EST_CELLS = 400; // Ước tính tổng số ô trên bản đồ

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — RESET (gọi khi bot chết)
    // ═══════════════════════════════════════════════════════════
    public void ResetMemory()
    {
        _visited.Clear();
        if (_unreachableCells != null) _unreachableCells.Clear();
        _ignoredItems.Clear();
        _healPOIs.Clear();
        _itemPOIs.Clear();
        _coinPOIs.Clear();
        _enemyPOIs.Clear();
        _pathHistory.Clear();
        _pathWaypoints.Clear();
        CurrentGoal       = GoalType.Explore;
        GoalPosition      = transform.position;
        FinalGoalPosition = transform.position;
        GoalUrgency       = 0.5f;
        IsLooping         = false;
        CoverageRatio     = 0f;
        _hasQueuedGoal    = false;
    }
    
    private bool _hasQueuedGoal = false;
    private GoalType _nextGoalType;
    private Vector2 _nextGoalPosition;
    private List<Vector2> _nextPathWaypoints;
    public List<MemoryPOI> GetItemPOIs() => _itemPOIs;
    public List<MemoryPOI> GetCoinPOIs() => _coinPOIs;

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — CẬP NHẬT mỗi Decision Step
    // ═══════════════════════════════════════════════════════════
    public void UpdateMemory(Vector2 botPos)
    {
        // 1. Đánh dấu vị trí hiện tại đã thăm
        Vector2Int cell = WorldToCell(botPos);
        _visited.Add(cell);
        CoverageRatio = Mathf.Clamp01((float)_visited.Count / MAP_EST_CELLS);

        // 2. Lưu lịch sử đường đi
        _pathHistory.Enqueue(botPos);
        while (_pathHistory.Count > pathHistoryLen)
            _pathHistory.Dequeue();

        // 3. Phát hiện lặp vòng
        IsLooping = KiemTraLap(botPos);

        // 4. Dọn POI hết hạn theo thời gian
        DonPOI(_healPOIs);
        DonPOI(_itemPOIs);
        DonPOI(_coinPOIs);
        DonPOI(_enemyPOIs);

        // 5. Dọn POI rác (Đã đi tới tận nơi thì chắc chắn đã ăn/mất)
        DonPOIGan(_coinPOIs, botPos, 1.5f);
        DonPOIGan(_itemPOIs, botPos, 1.5f);
        DonPOIGan(_enemyPOIs, botPos, 2.0f); // Xóa tọa độ địch cũ nếu đã đến tận nơi mà không thấy
    }

    private void DonPOIGan(List<MemoryPOI> list, Vector2 botPos, float pickRadius)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(list[i].worldPos, botPos) < pickRadius)
            {
                list.RemoveAt(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — THÊM ĐIỂM QUAN TÂM
    // ═══════════════════════════════════════════════════════════
    public void GhiNhanHealStation(Vector2 pos)
        => ThemPOI(_healPOIs, pos, POIType.HealStation, 1.0f);

    public void GhiNhanItem(Vector2 pos)
    {
        Vector2Int cell = WorldToCell(pos);
        if (_ignoredItems.Contains(cell)) return;

        bool daCo = false;
        foreach (var p in _itemPOIs)
        {
            if (Vector2.Distance(p.worldPos, pos) < cellSize)
            {
                daCo = true; 
                break;
            }
        }

        if (!daCo)
        {
            // [ĐÃ SỬA] Luôn nhặt đồ 100%, bỏ qua random
        }

        ThemPOI(_itemPOIs, pos, POIType.Item, 0.8f);
    }

    public void GhiNhanCoin(Vector2 pos)
        => ThemPOI(_coinPOIs, pos, POIType.Coin, 0.5f);

    public void GhiNhanDich(Vector2 pos)
        => ThemPOI(_enemyPOIs, pos, POIType.Enemy, 0.6f);

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — CẬP NHẬT MỤC TIÊU (logic ưu tiên)
    // ═══════════════════════════════════════════════════════════
    public void CapNhatMucTieu(Vector2 botPos, float healthRatio,
                               TankPlayer enemy, bool hasLOSToEnemy)
    {
        // ── KIỂM TRA XE BỊ KẸT CHẾT CỨNG (Stuck Time) ──
        // (Được gọi mỗi frame từ Agent)
        if (Vector2.Distance(botPos, _lastStuckPos) < 0.1f)
        {
            _stuckTime += Time.fixedDeltaTime;
            if (_stuckTime > 1.0f)
            {
                // Bị kẹt cứng 1s -> Xóa bỏ mục tiêu hiện tại để phân chia lại giai đoạn
                if (CurrentGoal == GoalType.Loot)
                {
                    if (_unreachableCells != null) _unreachableCells.Add(WorldToCell(FinalGoalPosition));
                    XoaPOI(FinalGoalPosition);
                }
                else if (CurrentGoal == GoalType.Explore)
                {
                    _visited.Add(WorldToCell(FinalGoalPosition));
                }
                
                ChonMucTieuMoi(botPos, healthRatio, enemy, hasLOSToEnemy);
                _stuckTime = 0f;
                return; // Ngừng tính toán thêm frame này
            }
        }
        else
        {
            _stuckTime = 0f;
            _lastStuckPos = botPos;
        }

        int currentCoins = 0;
        var wallet = GetComponent<CoinWallet>();
        if (wallet != null) currentCoins = wallet.TotalCoins.Value;
        
        int chiPhiBan = 5;
        var bpd = GetComponent<BoPhongDan>();
        if (bpd != null) chiPhiBan = bpd.GetChiPhiBan();

        // Tìm xem còn nhớ vị trí địch gần đây không (trong vòng 3 giây)
        bool enemyLostForLongTime = true;
        if (!hasLOSToEnemy)
        {
            var recentEnemy = TimPOITotNhat(_enemyPOIs, botPos, null);
            if (recentEnemy != null && (Time.time - recentEnemy.Value.timeAdded) < 3.0f)
            {
                enemyLostForLongTime = false; // Vẫn còn nhớ vị trí địch, tiếp tục giao tranh/flank
            }
        }
        else
        {
            enemyLostForLongTime = false;
        }

        // Thoát khỏi Combat/Retreat nếu kẻ địch đã chết HOẶC khuất tầm nhìn quá 3 giây
        if ((enemy == null || enemyLostForLongTime) && (CurrentGoal == GoalType.Combat || CurrentGoal == GoalType.Retreat))
        {
            CurrentGoal = GoalType.Explore;
            ChonMucTieuMoi(botPos, healthRatio, enemy, hasLOSToEnemy);
            return;
        }

        // Đang giao tranh mà HẾT ĐIỂM BẮN -> Bắt buộc chuồn đi nhặt coin!
        if (CurrentGoal == GoalType.Combat && currentCoins < chiPhiBan)
        {
            CurrentGoal = GoalType.Explore;
            ChonMucTieuMoi(botPos, healthRatio, enemy, hasLOSToEnemy);
            return;
        }

        // ── CƠ CHẾ BÁM TRẠNG THÁI (Hysteresis) ──
        // Nếu đang trong quá trình đi hồi máu hoặc đang đứng hồi máu, cấm bị xao nhãng cho tới khi đạt 80% máu
        bool isHysteresisHeal = (CurrentGoal == GoalType.Heal && healthRatio < 0.8f);

        // Bị kỳ đà cản mũi khi đang hồi máu
        if (isHysteresisHeal && enemy != null && hasLOSToEnemy)
        {
            float dist = Vector2.Distance(botPos, enemy.transform.position);
            if (dist < 8f) // Địch mò tới tận trạm hồi máu
            {
                Mau enemyHealth = enemy.GetComponent<Mau>();
                float enemyRatio = enemyHealth != null ? (float)enemyHealth.MauHienTai.Value / enemyHealth.MauToiDa : 1.0f;
                
                if (healthRatio < enemyRatio)
                {
                    // Đang yếu máu hơn mà nó đuổi tới -> Bỏ trạm hồi máu, Rút lui khẩn cấp!
                    Vector2 safeNode = AStarPathfinding.Instance.FindSafeRetreatNode(botPos, enemy.transform.position);
                    DatMucTieu(GoalType.Retreat, safeNode, 0.9f, botPos, enemy.transform.position);
                    return;
                }
                else if (currentCoins >= chiPhiBan)
                {
                    // Máu mình bằng hoặc trâu hơn nó -> Không thèm hồi nữa, quay ra đấm nó luôn!
                    DatMucTieu(GoalType.Combat, enemy.transform.position, 1.0f, botPos);
                    return;
                }
            }
        }

        if (!isHysteresisHeal)
        {
            // ── Ưu tiên TỐI THƯỢNG: Sắp tông vào địch -> Bắt buộc Giao tranh để lách vòng tròn ──
            if (enemy != null && hasLOSToEnemy && Vector2.Distance(botPos, enemy.transform.position) < 2.5f)
            {
                // Cập nhật vị trí địch liên tục, không return để code chạy tiếp xuống phần chuyển trạm
                DatMucTieu(GoalType.Combat, enemy.transform.position, 1.0f, botPos);
            }
            // Kiểm tra lại điều kiện ưu tiên cao: Máu cực thấp (và phải đủ tiền viện phí)
            else if (healthRatio < healThreshold && currentCoins >= 10 && TimPOITotNhat(_healPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null) != null)
            {
                if (CurrentGoal != GoalType.Heal) 
                    DatMucTieu(GoalType.Heal, TimPOITotNhat(_healPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null).Value.worldPos, 1.0f, botPos);
            }
            // Kiểm tra ưu tiên cao: Địch ngay trước mặt (Chỉ khi CÓ TIỀN BẮN ĐẠN)
            else if (enemy != null && hasLOSToEnemy && healthRatio >= healThreshold && currentCoins >= chiPhiBan)
            {
                // Liên tục track theo địch
                DatMucTieu(GoalType.Combat, enemy.transform.position, 0.8f, botPos);
            }
        }

        // Chuyển trạm (Waypoint) hoặc Đích đến (Goal)
        bool passedWaypoint = false;
        Vector2 toWaypoint = GoalPosition - botPos;
        
        // [FIXED] Tăng khoảng cách "chạm đích" lên 0.6f cho Loot để bot dễ dàng ăn coin/item mà không cần đâm đầu sát rạt tường
        float currentReachedDist = (CurrentGoal == GoalType.Loot || CurrentGoal == GoalType.Heal) && _pathWaypoints.Count <= 1 ? 0.6f : goalReachedDist;

        // ── THUẬT TOÁN PHÂN CHIA GIAI ĐOẠN DI CHUYỂN THÔNG MINH (Smart Corner Cutting) ──
        if (toWaypoint.magnitude < currentReachedDist)
        {
            // Mặc định cho phép cắt cua (chuyển trạm)
            passedWaypoint = true;
            
            // Tối ưu hóa: Nếu còn trạm tiếp theo, kiểm tra xem có an toàn để cắt cua sớm không
            if (_pathWaypoints != null && _pathWaypoints.Count > 1)
            {
                // Dùng tia CircleCast bán kính TO (0.45m ~ bằng thân xe) để đảm bảo an toàn tuyệt đối khi rẽ
                if (AStarPathfinding.Instance != null && !AStarPathfinding.Instance.CheckLineOfSight(botPos, _pathWaypoints[1], 0.45f))
                {
                    // Nếu Tầm Nhìn bị che (nghĩa là cắt cua bây giờ sẽ tông tường!)
                    // BẮT BUỘC xe phải đi sát vào trạm hiện tại (< 0.4m) để ôm sát cua một cách an toàn
                    if (toWaypoint.magnitude > 0.4f)
                    {
                        passedWaypoint = false; 
                    }
                }
            }
        }
        else
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.magnitude > 0.5f)
            {
                float dot = Vector2.Dot(rb.velocity.normalized, toWaypoint.normalized);
                // Lỡ trớn vượt qua trạm (khoảng cách < 2.5m nhưng hướng chạy ngược lại)
                if (toWaypoint.magnitude < 2.5f && dot < -0.2f)
                {
                    passedWaypoint = true;
                }
            }
        }
        
        // ── TÍNH NĂNG TIÊN TRI ĐƯỜNG ĐI (Look-ahead Queue) ──
        if (_pathWaypoints != null && _pathWaypoints.Count <= 1 && !_hasQueuedGoal)
        {
            float distToFinal = Vector2.Distance(botPos, FinalGoalPosition);
            // Thiết kế riêng theo yêu cầu: Khi đang di chuyển đến mục tiêu và bán kính còn 20m thì tìm luôn mục tiêu tiếp theo
            if (distToFinal <= 20f && distToFinal > 1.5f)
            {
                // Chọn mục tiêu tiếp theo BẮT ĐẦU từ đích đến hiện tại (FinalGoalPosition)
                ChuanBiMucTieuTiepTheo(FinalGoalPosition, healthRatio, enemy, hasLOSToEnemy);
            }
        }

        if (passedWaypoint || IsLooping)
        {
            if (IsLooping) _pathHistory.Clear(); // Xóa lịch sử để ngắt vòng lặp vô tận, cho phép timer đếm ngược

            if (_pathWaypoints != null && _pathWaypoints.Count > 0)
            {
                _pathWaypoints.RemoveAt(0);
                if (_pathWaypoints.Count > 0)
                {
                    GoalPosition = _pathWaypoints[0]; // Cập nhật trạm trung chuyển tiếp theo
                    return;
                }
            }

            // Đã đến trạm cuối cùng của đường đi (hoặc kẹt)!
            if (CurrentGoal == GoalType.Loot)
            {
                // Loot rớt trên mặt đất, nếu đi đến nơi rồi mà hàm này vẫn chạy -> kẹt vô góc hoặc xuyên tường
                XoaPOI(FinalGoalPosition);
            }
            else if (CurrentGoal == GoalType.Explore)
            {
                // Đích khám phá bị kẹt -> ép vào list đã thăm
                _visited.Add(WorldToCell(FinalGoalPosition));
            }

            // CƠ CHẾ BÁM TRẠNG THÁI HỒI MÁU (Hysteresis)
            if (CurrentGoal == GoalType.Heal && healthRatio < 0.8f)
            {
                // Vẫn đang cần hồi máu, KHÔNG chọn mục tiêu mới
                GoalPosition = FinalGoalPosition; // Đứng yên tại chỗ
                return;
            }

            // Hết trạm trung chuyển -> Kiểm tra hàng đợi mục tiêu (Queued Target)
            if (_hasQueuedGoal)
            {
                // Lập tức áp dụng mục tiêu tiếp theo đã tính sẵn từ trước (Không mất frame suy nghĩ)
                CurrentGoal = _nextGoalType;
                FinalGoalPosition = _nextGoalPosition;
                _pathWaypoints = _nextPathWaypoints;
                _hasQueuedGoal = false;

                if (_pathWaypoints != null && _pathWaypoints.Count > 0)
                {
                    GoalPosition = _pathWaypoints[0];
                }
                else
                {
                    GoalPosition = FinalGoalPosition;
                }
                return;
            }

            // Nếu không có hàng đợi (do kẹt hoặc lỗi) -> Chọn lại đích đến hoàn toàn mới
            ChonMucTieuMoi(botPos, healthRatio, enemy, hasLOSToEnemy);
            return;
        }
    }

    private void ChonMucTieuMoi(Vector2 botPos, float healthRatio,
                                TankPlayer enemy, bool hasLOSToEnemy)
    {
        int currentCoins = 0;
        var wallet = GetComponent<CoinWallet>();
        if (wallet != null) currentCoins = wallet.TotalCoins.Value;
        
        int chiPhiBan = 5;
        var bpd = GetComponent<BoPhongDan>();
        if (bpd != null) chiPhiBan = bpd.GetChiPhiBan();

        // ── Ưu tiên 1: MÁU THẤP -> CỨU MẠNG TRƯỚC ─────────────────────────────────
        // [UPDATED] Đã vá lỗi tham tiền để bị bắn khi yếu máu!
        if (healthRatio < healThreshold)
        {
            if (currentCoins >= 10)
            {
                var heal = TimPOITotNhat(_healPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null);
                if (heal != null)
                {
                    DatMucTieu(GoalType.Heal, heal.Value.worldPos, 1.0f, botPos);
                    return;
                }
            }

            // Máu thấp, không thể hồi máu -> CHẠY NGAY ĐI nếu địch ở gần hoặc đang nhìn thấy mình
            if (enemy != null && !IsLooping && (hasLOSToEnemy || Vector2.Distance(botPos, enemy.transform.position) < 8f))
            {
                Vector2 safeNode = AStarPathfinding.Instance.FindSafeRetreatNode(botPos, enemy.transform.position);
                DatMucTieu(GoalType.Retreat, safeNode, 0.9f, botPos, enemy.transform.position);
                return;
            }

            // Đã an toàn (xa địch/khuất tầm nhìn) -> Đi nhặt tiền để chuẩn bị hồi máu
            var urgentCoin = TimPOITotNhat(_coinPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null);
            if (urgentCoin != null)
            {
                DatMucTieu(GoalType.Loot, urgentCoin.Value.worldPos, 1.0f, botPos);
                return;
            }
        }

        // ── Ưu tiên 1.5: HẾT ĐẠN THÌ TÌM COIN NGAY LẬP TỨC (Hoặc bỏ chạy) ──
        if (currentCoins < chiPhiBan)
        {
            // Hết đạn + Địch đang ép sát/Nhìn thấy -> RÚT LUI NGAY! (Không tham nhặt tiền để bị bắn)
            if (enemy != null && !IsLooping && (hasLOSToEnemy || Vector2.Distance(botPos, enemy.transform.position) < 8f))
            {
                Vector2 safeNode = AStarPathfinding.Instance.FindSafeRetreatNode(botPos, enemy.transform.position);
                DatMucTieu(GoalType.Retreat, safeNode, 0.9f, botPos, enemy.transform.position);
                return;
            }

            // Đã an toàn -> Đi nhặt tiền nạp đạn
            var urgentCoin = TimPOITotNhat(_coinPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null);
            if (urgentCoin != null)
            {
                DatMucTieu(GoalType.Loot, urgentCoin.Value.worldPos, 1.0f, botPos);
                return;
            }
            else
            {
                // [CHEAT/HACK] Hết đạn mà bộ nhớ trống rỗng -> Bật Radar quét toàn bản đồ tìm Coin ngay lập tức!
                Coin[] allCoins = GameObject.FindObjectsOfType<Coin>();
                Coin nearestCoin = null;
                float minD = float.MaxValue;
                foreach (var c in allCoins)
                {
                    if (c == null || !c.gameObject.activeInHierarchy) continue;
                    if (_unreachableCells != null && _unreachableCells.Contains(WorldToCell(c.transform.position))) continue;
                    
                    float d = Vector2.Distance(botPos, c.transform.position);
                    if (d < minD)
                    {
                        minD = d;
                        nearestCoin = c;
                    }
                }
                
                if (nearestCoin != null)
                {
                    GhiNhanCoin(nearestCoin.transform.position);
                    DatMucTieu(GoalType.Loot, nearestCoin.transform.position, 1.0f, botPos);
                    return;
                }
            }
        }

        // ── Ưu tiên 2: GIAO TRANH (Chỉ khi có đủ tiền đạn) ──────────
        if (enemy != null && hasLOSToEnemy && healthRatio >= healThreshold && currentCoins >= chiPhiBan)
        {
            // ── [UPDATED] CHIẾN THUẬT TRIỆT HẠ TÀI NGUYÊN (Resource Denial) ──
            // Nếu địch ở xa (>20m) mà có đồ xịn ở ngay chân (<10m), thà cướp đồ để nó khỏi ăn!
            float distToEnemy = Vector2.Distance(botPos, enemy.transform.position);
            if (distToEnemy > 20f)
            {
                var denyItem = TimPOIGanNhat(_itemPOIs, botPos); // Vẫn dùng hàm cũ cho đồ rơi
                if (denyItem != null && Vector2.Distance(botPos, denyItem.Value.worldPos) < 10f)
                {
                    DatMucTieu(GoalType.Loot, denyItem.Value.worldPos, 1.0f, botPos);
                    return;
                }
                var denyHeal = TimPOIGanNhat(_healPOIs, botPos);
                if (denyHeal != null && Vector2.Distance(botPos, denyHeal.Value.worldPos) < 10f)
                {
                    DatMucTieu(GoalType.Loot, denyHeal.Value.worldPos, 1.0f, botPos);
                    return;
                }
            }

            DatMucTieu(GoalType.Combat, enemy.transform.position, 0.8f, botPos);
            return;
        }

        // ── Ưu tiên 3: ĐỊA CHỈNH CUỐI CÙNG BIẾT CỦA ĐỊCH (Chỉ khi có đủ tiền) ────
        if (enemy != null && !hasLOSToEnemy && currentCoins >= chiPhiBan)
        {
            var enemyMem = TimPOIGanNhat(_enemyPOIs, botPos);
            if (enemyMem != null)
            {
                // Thay vì Combat, dùng Explore để bám theo đường A* thay vì múa vòng tròn
                DatMucTieu(GoalType.Explore, enemyMem.Value.worldPos, 0.6f, botPos);
                return;
            }
        }

        // ── Ưu tiên 5: NHẶT ĐỒ GẦN NHẤT KHI RẢNH RỖI ──
        var closestItemMem = TimPOIGanNhat(_itemPOIs, botPos);
        var closestCoinMem = TimPOIGanNhat(_coinPOIs, botPos);
        MemoryPOI? bestLoot = null;
        if (closestItemMem != null && closestCoinMem != null)
        {
            if (Vector2.Distance(botPos, closestItemMem.Value.worldPos) < Vector2.Distance(botPos, closestCoinMem.Value.worldPos))
                bestLoot = closestItemMem;
            else
                bestLoot = closestCoinMem;
        }
        else if (closestItemMem != null) bestLoot = closestItemMem;
        else if (closestCoinMem != null) bestLoot = closestCoinMem;

        if (bestLoot != null)
        {
            DatMucTieu(GoalType.Loot, bestLoot.Value.worldPos, 0.5f, botPos);
            return;
        }

        // ── Ưu tiên 6: KHÁM PHÁ CÓ CHỈ HƯỚNG ────────────────
        // Nếu đang khám phá và mục tiêu hiện tại vẫn còn xa, tiếp tục giữ nguyên để đi hết đường! (Tránh cà giật do đổi mục tiêu mỗi 0.5s)
        if (CurrentGoal == GoalType.Explore && Vector2.Distance(botPos, FinalGoalPosition) > 5f)
        {
            return; // Giữ nguyên mục tiêu khám phá hiện tại
        }

        // Tìm điểm khám phá mới (từ 50-200m)
        Vector2 exploTarget = TimOChuaTham(botPos);
        DatMucTieu(GoalType.Explore, exploTarget, 0.3f, botPos);
    }

    private void ChuanBiMucTieuTiepTheo(Vector2 startPos, float healthRatio, TankPlayer enemy, bool hasLOSToEnemy)
    {
        // Chạy logic chọn mục tiêu y như thật, nhưng thay vì ghi đè vào mục tiêu hiện tại (DatMucTieu),
        // nó sẽ lưu vào biến hàng đợi (_next...)
        
        Vector2 nextGoalPos = TimOChuaTham(startPos);
        GoalType nextGoalType = GoalType.Explore;

        // Ưu tiên nhặt đồ gần nhất
        var nearestItem = TimPOIGanNhat(_itemPOIs, startPos);
        var nearestCoin = TimPOIGanNhat(_coinPOIs, startPos);
        MemoryPOI? bestLoot = null;
        if (nearestItem != null && nearestCoin != null)
        {
            if (Vector2.Distance(startPos, nearestItem.Value.worldPos) < Vector2.Distance(startPos, nearestCoin.Value.worldPos))
                bestLoot = nearestItem;
            else
                bestLoot = nearestCoin;
        }
        else if (nearestItem != null) bestLoot = nearestItem;
        else if (nearestCoin != null) bestLoot = nearestCoin;

        if (bestLoot != null)
        {
            nextGoalPos = bestLoot.Value.worldPos;
            nextGoalType = GoalType.Loot;
        }
        
        _nextGoalType = nextGoalType;
        _nextGoalPosition = nextGoalPos;
        _nextPathWaypoints = AStarPathfinding.Instance.FindPath(startPos, nextGoalPos);
        _hasQueuedGoal = true;
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — LẤY OBSERVATIONS để đưa vào ML model
    //  Trả về 22 giá trị:
    //  [0-4]  = GoalType one-hot (Heal/Combat/Loot/Explore/Retreat)
    //  [5-6]  = Goal direction local (x, y chuẩn hóa)
    //  [7]    = Goal distance / 30m
    //  [8]    = Goal urgency
    //  [9]    = IsLooping
    //  [10]   = CoverageRatio
    //  [11-13]= Heal POI gần nhất (có/dx/dy local)
    //  [14-16]= Item POI gần nhất (có/dx/dy local)
    //  [17-19]= Coin POI gần nhất (có/dx/dy local)
    //  [20-22]= Enemy last known (có/dx/dy local)
    // ═══════════════════════════════════════════════════════════
    public float[] GetObservations(Vector2 botPos, Transform botTransform)
    {
        float[] obs = new float[23];
        int idx = 0;

        // [0-4] Goal type one-hot
        obs[idx + (int)CurrentGoal] = 1f;
        idx += 5;

        // [5-6] Goal direction local
        Vector2 goalLocal = botTransform.InverseTransformPoint(GoalPosition);
        obs[idx++] = Mathf.Clamp(goalLocal.x / 30f, -1f, 1f);
        obs[idx++] = Mathf.Clamp(goalLocal.y / 30f, -1f, 1f);

        // [7] Goal distance
        obs[idx++] = Mathf.Clamp01(Vector2.Distance(botPos, GoalPosition) / 30f);

        // [8] Goal urgency
        obs[idx++] = GoalUrgency;

        // [9] Looping
        obs[idx++] = IsLooping ? 1f : 0f;

        // [10] Map coverage
        obs[idx++] = CoverageRatio;

        // [11-13] Nearest Heal POI
        idx = ThemPOIObs(obs, idx, _healPOIs, botPos, botTransform);

        // [14-16] Nearest Item POI
        idx = ThemPOIObs(obs, idx, _itemPOIs, botPos, botTransform);

        // [17-19] Nearest Coin POI
        idx = ThemPOIObs(obs, idx, _coinPOIs, botPos, botTransform);

        // [20-22] Last known Enemy
        idx = ThemPOIObs(obs, idx, _enemyPOIs, botPos, botTransform);

        return obs;
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — Ô đã thăm chưa?
    // ═══════════════════════════════════════════════════════════
    public bool DaThum(Vector2 worldPos)
        => _visited.Contains(WorldToCell(worldPos));

    // ═══════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════
    private float _lastPathCalcTime = 0f;
    private Vector2 _lastTargetPos = Vector2.zero;

    private Vector2Int WorldToCell(Vector2 worldPos)
        => new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.y / cellSize));

    private void ThemPOI(List<MemoryPOI> list, Vector2 pos, POIType type, float importance)
    {
        // Kiểm tra đã có POI gần đó chưa (cập nhật thay vì thêm trùng)
        for (int i = 0; i < list.Count; i++)
        {
            if (Vector2.Distance(list[i].worldPos, pos) < cellSize)
            {
                var upd = list[i];
                upd.timeAdded  = Time.time;
                upd.importance = importance;
                upd.worldPos   = pos;
                list[i] = upd;
                return;
            }
        }
        // Thêm mới
        list.Add(new MemoryPOI
        {
            worldPos   = pos,
            type       = type,
            timeAdded  = Time.time,
            importance = importance
        });
        // Giữ tối đa maxPOI
        if (list.Count > maxPOI)
            list.RemoveAt(0);
    }

    public void XoaPOI(Vector2 pos)
    {
        _healPOIs.RemoveAll(p => Vector2.Distance(p.worldPos, pos) < 0.5f);
        _itemPOIs.RemoveAll(p => Vector2.Distance(p.worldPos, pos) < 0.5f);
        _coinPOIs.RemoveAll(p => Vector2.Distance(p.worldPos, pos) < 0.5f);
        _enemyPOIs.RemoveAll(p => Vector2.Distance(p.worldPos, pos) < 0.5f);
    }

    private void DonPOI(List<MemoryPOI> list)
    {
        list.RemoveAll(p => !p.IsStillValid(poiMaxAge));
    }

        private MemoryPOI? TimPOITotNhat(List<MemoryPOI> list, Vector2 botPos, Vector2? enemyPos = null)
    {
        if (list == null || list.Count == 0) return null;
        MemoryPOI? best = null;
        float bestScore = float.MaxValue;
        foreach (var p in list)
        {
            if (_unreachableCells != null && _unreachableCells.Contains(WorldToCell(p.worldPos))) continue;
            float score = Vector2.Distance(botPos, p.worldPos);
            // Áp dụng Risk Penalty nếu có kẻ địch
            if (enemyPos.HasValue)
            {
                float distToEnemy = Vector2.Distance(p.worldPos, enemyPos.Value);
                if (distToEnemy < 8f)
                {
                    score += (8f - distToEnemy) * 15f; // Phạt cực nặng nếu item nằm sát địch
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = p;
            }
        }
        return best;
    }

    public MemoryPOI? TimPOIGanNhat(List<MemoryPOI> list, Vector2 botPos)
    {
        MemoryPOI? best = null;
        float minDist = float.MaxValue;
        foreach (var p in list)
        {
            if (!p.IsStillValid(poiMaxAge)) continue;
            if (_unreachableCells != null && _unreachableCells.Contains(WorldToCell(p.worldPos))) continue;
            float d = Vector2.Distance(p.worldPos, botPos);
            if (d < minDist) { minDist = d; best = p; }
        }
        return best;
    }

    private bool KiemTraLap(Vector2 botPos)
    {
        // Kiểm tra xem bot có đang quay vòng trong bán kính nhỏ không
        if (_pathHistory.Count < loopDetectWindow) return false;

        Vector2[] hist = new List<Vector2>(_pathHistory).ToArray();
        int count = 0;
        // So sánh nửa đầu với nửa cuối history
        for (int i = 0; i < loopDetectWindow / 2; i++)
        {
            int j = hist.Length - loopDetectWindow / 2 + i;
            if (j >= 0 && j < hist.Length &&
                Vector2.Distance(hist[i], hist[j]) < loopDetectRadius)
                count++;
        }
        return count > loopDetectWindow / 3;
    }

    /// Tìm ô chưa thăm gần nhất (khám phá có chỉ hướng)
    private Vector2 TimOChuaTham(Vector2 botPos)
    {
        // MỤC TIÊU DI CHUYỂN PHẢI TỪ 10-50m (Theo đúng yêu cầu để bot không chạy quá xa)
        float randomDist = Random.Range(10f, 50f);
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector2 finalPos = botPos + randomDirection * randomDist;
        
        // [FIXED] Đã XÓA Raycast! Raycast khiến bot bị giới hạn bởi lớp vật cản đầu tiên nên không bao giờ đi xa được.
        // Chức năng chống đặt mục tiêu ra ngoài map đã được GridManager (FindNearestWalkableNode) tự động ép mục tiêu về điểm an toàn.
        
        return finalPos;
    }

    public void SetCombatTarget(Vector2 targetPos)
    {
        DatMucTieu(GoalType.Combat, targetPos, 1.0f, transform.position);
    }

    private void DatMucTieu(GoalType type, Vector2 finalPos, float urgency, Vector2 botPos, Vector2? threatPos = null)
    {
        if (GridManager.Instance != null)
        {
            Vector2 center = GridManager.Instance.transform.position;
            Vector2 size = GridManager.Instance.gridWorldSize;
            float clampedX = Mathf.Clamp(finalPos.x, center.x - size.x / 2f + 2.5f, center.x + size.x / 2f - 2.5f);
            float clampedY = Mathf.Clamp(finalPos.y, center.y - size.y / 2f + 2.5f, center.y + size.y / 2f - 2.5f);
            finalPos = new Vector2(clampedX, clampedY);
            
            // [FIXED] Ép mục tiêu phải NẰM NGOÀI ĐÁ/TƯỜNG. Nếu rớt trúng đá, tự dời ra ô đất trống gần nhất.
            // Điều này sửa triệt để lỗi "vạch trắng chĩa vào trong đá" hoặc "chĩa ra ngoài biên giới".
            Node targetNode = GridManager.Instance.NodeFromWorldPoint(finalPos);
            if (!targetNode.walkable && AStarPathfinding.Instance != null)
            {
                Node safeNode = AStarPathfinding.Instance.FindNearestWalkableNode(targetNode);
                if (safeNode != null)
                {
                    finalPos = safeNode.worldPosition;
                }
            }
        }

        CurrentGoal       = type;
        FinalGoalPosition = finalPos;
        GoalUrgency       = urgency;

        if (type == GoalType.Combat)
        {
            // [UPDATED] Dùng A* trong giao tranh để vẽ đường chiến lược chia thành nhiều phần tránh tường
            if (Time.time - _lastPathCalcTime < 0.25f && Vector2.Distance(_lastTargetPos, finalPos) < 1f)
            {
                // Tránh spam A* liên tục
                if (_pathWaypoints == null || _pathWaypoints.Count == 0) GoalPosition = finalPos;
                return;
            }
            _lastPathCalcTime = Time.time;
            _lastTargetPos = finalPos;
            
            if (AStarPathfinding.Instance != null)
            {
                _pathWaypoints = AStarPathfinding.Instance.FindPath(botPos, finalPos);
                if (_pathWaypoints != null && _pathWaypoints.Count > 0)
                {
                    // [FIXED] Tự động dời đích đến về điểm dừng cuối cùng nếu đích thật bị nằm ngoài biên giới
                    Vector2 lastWaypoint = _pathWaypoints[_pathWaypoints.Count - 1];
                    if (Vector2.Distance(lastWaypoint, finalPos) > 1.5f)
                    {
                        finalPos = lastWaypoint;
                        FinalGoalPosition = finalPos;
                    }
                    GoalPosition = _pathWaypoints[0];
                }
                else
                {
                    GoalPosition = finalPos;
                }
            }
            else
            {
                GoalPosition = finalPos;
            }
        }
        else
        {
            // Cooldown chống Spam đường đi A*
            if (Time.time - _lastPathCalcTime < 0.5f && Vector2.Distance(_lastTargetPos, finalPos) < 2f)
            {
                if (_pathWaypoints != null && _pathWaypoints.Count > 0)
                {
                    GoalPosition = _pathWaypoints[0];
                    return;
                }
            }

            _lastPathCalcTime = Time.time;
            _lastTargetPos = finalPos;

            // Tuần tra, Khám phá, Hồi máu, Nhặt đồ -> Sử dụng não A* để lập kế hoạch di chuyển
            if (AStarPathfinding.Instance != null)
            {
                _pathWaypoints = AStarPathfinding.Instance.FindPath(botPos, finalPos);
                
                if (_pathWaypoints != null && _pathWaypoints.Count > 0)
                {
                    // [FIXED] Ép mục tiêu (Explore/Loot) rớt ngoài map/trong đá tự động thu về ô an toàn cuối cùng
                    Vector2 lastWaypoint = _pathWaypoints[_pathWaypoints.Count - 1];
                    if (Vector2.Distance(lastWaypoint, finalPos) > 1.5f)
                    {
                        finalPos = lastWaypoint;
                        FinalGoalPosition = finalPos;
                    }
                    GoalPosition = _pathWaypoints[0]; // Điểm trung chuyển đầu tiên
                }
                else if (_pathWaypoints == null)
                {
                    // LỖI TÌM ĐƯỜNG (A* trả về null): Vị trí đích hoàn toàn bị tường bít kín không thể đi tới
                    GoalPosition = botPos; // Dừng lại để lập tức chọn mục tiêu mới
                    FinalGoalPosition = botPos; // [FIXED] Kéo vạch trắng (đích) thu hồi về sát xe để tránh lỗi hiển thị kẹt vào trong tường
                    if (_unreachableCells != null) _unreachableCells.Add(WorldToCell(finalPos)); // Danh sách đen vĩnh viễn!
                    
                    if (type == GoalType.Explore)
                    {
                        // Đánh dấu ô lỗi này là 'đã thăm' để TimOChuaTham không chọn lại nữa
                        _visited.Add(WorldToCell(finalPos));
                    }
                    else
                    {
                        // Rác (Coin/Item rớt trong kẹt đá) -> Xóa khỏi bộ nhớ để tránh lặp vô hạn
                        XoaPOI(finalPos);
                    }
                    
                    // [FIXED] Chủ động kích hoạt cơ chế gỡ kẹt để buộc xe phải lấy mục tiêu mới ở frame sau!
                    _stuckTime = 2.0f;
                }
                else
                {
                    // Trường hợp Count == 0 (đã ở chung 1 ô Grid với mục tiêu) -> Cứ chạy thẳng tới đích
                    GoalPosition = finalPos; 
                }
            }
            else
            {
                GoalPosition = finalPos;
            }
        }
    }

    private int ThemPOIObs(float[] obs, int idx, List<MemoryPOI> list,
                           Vector2 botPos, Transform botTransform)
    {
        var poi = TimPOIGanNhat(list, botPos);
        if (poi != null)
        {
            obs[idx++] = 1f; // Có POI
            Vector2 local = botTransform.InverseTransformPoint(poi.Value.worldPos);
            obs[idx++] = Mathf.Clamp(local.x / 30f, -1f, 1f);
            obs[idx++] = Mathf.Clamp(local.y / 30f, -1f, 1f);
        }
        else
        {
            obs[idx++] = 0f;
            obs[idx++] = 0f;
            obs[idx++] = 0f;
        }
        return idx;
    }

    // ─── GIZMOS — Hiển thị trong Scene ──────────────────────────
    private void OnDrawGizmos()
    {
        // Vẽ kế hoạch di chuyển (A* Pathfinding)
        if (_pathWaypoints != null && _pathWaypoints.Count > 0)
        {
            Gizmos.color = Color.green;
            Vector2 prevWaypoint = transform.position;
            foreach (var wp in _pathWaypoints)
            {
                Gizmos.DrawLine(prevWaypoint, wp);
                Gizmos.DrawSphere(wp, 0.2f);
                prevWaypoint = wp;
            }
        }

        // Mục tiêu hiện tại
        Gizmos.color = CurrentGoal switch
        {
            GoalType.Heal    => Color.green,
            GoalType.Combat  => Color.red,
            GoalType.Loot    => Color.yellow,
            GoalType.Retreat => Color.magenta,
            _                => Color.white
        };
        Gizmos.DrawWireSphere(FinalGoalPosition, 1.2f);
        Gizmos.DrawLine(transform.position, GoalPosition);

        // Vẽ các POI được nhớ
        Gizmos.color = new Color(0, 1, 0, 0.6f);
        foreach (var p in _healPOIs) Gizmos.DrawSphere(p.worldPos, 0.4f);

        Gizmos.color = new Color(1, 1, 0, 0.6f);
        foreach (var p in _coinPOIs) Gizmos.DrawSphere(p.worldPos, 0.3f);

        Gizmos.color = new Color(1, 0.5f, 0, 0.6f);
        foreach (var p in _itemPOIs) Gizmos.DrawSphere(p.worldPos, 0.4f);

        // Vẽ lịch sử đường đi
        Gizmos.color = new Color(1, 0, 1, 0.4f);
        Vector2? prev = null;
        foreach (var p in _pathHistory)
        {
            if (prev.HasValue) Gizmos.DrawLine(prev.Value, p);
            prev = p;
        }

        // Vùng lặp vòng
        if (IsLooping)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, loopDetectRadius);
        }
    }
}
