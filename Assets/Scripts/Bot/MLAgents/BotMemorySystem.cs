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
    private List<MemoryPOI>       _healPOIs    = new List<MemoryPOI>();
    private List<MemoryPOI>       _itemPOIs    = new List<MemoryPOI>();
    private List<MemoryPOI>       _coinPOIs    = new List<MemoryPOI>();
    private List<MemoryPOI>       _enemyPOIs   = new List<MemoryPOI>();

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
    }

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
        => ThemPOI(_itemPOIs, pos, POIType.Item, 0.8f);

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
                if (CurrentGoal != GoalType.Combat) DatMucTieu(GoalType.Combat, enemy.transform.position, 1.0f, botPos);
                return;
            }

            // Kiểm tra lại điều kiện ưu tiên cao: Máu cực thấp (và phải đủ tiền viện phí)
            if (healthRatio < healThreshold && currentCoins >= 10 && TimPOITotNhat(_healPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null) != null && CurrentGoal != GoalType.Heal)
            {
                DatMucTieu(GoalType.Heal, TimPOITotNhat(_healPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null).Value.worldPos, 1.0f, botPos);
                return;
            }

            // Kiểm tra ưu tiên cao: Địch ngay trước mặt (Chỉ khi CÓ TIỀN BẮN ĐẠN)
            if (enemy != null && hasLOSToEnemy && healthRatio >= healThreshold && currentCoins >= chiPhiBan)
            {
                if (CurrentGoal != GoalType.Combat) DatMucTieu(GoalType.Combat, enemy.transform.position, 0.8f, botPos);
                return;
            }
        }

        // Chuyển trạm (Waypoint) hoặc Đích đến (Goal)
        bool passedWaypoint = false;
        Vector2 toWaypoint = GoalPosition - botPos;
        float currentReachedDist = (_pathWaypoints != null && _pathWaypoints.Count > 1) ? 1.5f : goalReachedDist;
        
        if (toWaypoint.magnitude < currentReachedDist)
        {
            passedWaypoint = true;
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

            // Hết trạm trung chuyển -> Chọn lại đích đến hoàn toàn mới
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

        // ── Ưu tiên 4: NHẶT ITEM ──────────────────────────────
        var item = TimPOIGanNhat(_itemPOIs, botPos);
        if (item != null)
        {
            DatMucTieu(GoalType.Loot, item.Value.worldPos, 0.7f, botPos);
            return;
        }

        // ── Ưu tiên 5: NHẶT COIN ──────────────────────────────
        var coin = TimPOITotNhat(_coinPOIs, botPos, enemy != null ? (Vector2)enemy.transform.position : null);
        if (coin != null)
        {
            DatMucTieu(GoalType.Loot, coin.Value.worldPos, 0.4f, botPos);
            return;
        }

        // ── Ưu tiên 6: KHÁM PHÁ CÓ CHỈ HƯỚNG ────────────────
        // Tìm ô lưới gần nhất chưa được thăm
        Vector2 exploTarget = TimOChuaTham(botPos);
        DatMucTieu(GoalType.Explore, exploTarget, 0.3f, botPos);
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

    private void XoaPOI(Vector2 pos)
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

    private MemoryPOI? TimPOIGanNhat(List<MemoryPOI> list, Vector2 botPos)
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
        // Quét lưới xung quanh từ gần ra xa
        List<Vector2Int> unvisitedCells = new List<Vector2Int>();
        for (int radius = 1; radius <= Mathf.RoundToInt(explorationRange / cellSize); radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                    Vector2Int cell = WorldToCell(botPos) + new Vector2Int(dx, dy);
                    if (!_visited.Contains(cell))
                    {
                        unvisitedCells.Add(cell);
                    }
                }
            }
            if (unvisitedCells.Count > 0)
            {
                Vector2Int chosenCell = unvisitedCells[Random.Range(0, unvisitedCells.Count)];
                return new Vector2(chosenCell.x * cellSize, chosenCell.y * cellSize);
            }
        }
        // Nếu đã đi hết vùng gần → đặt điểm ngẫu nhiên xa hơn
        return botPos + Random.insideUnitCircle.normalized * explorationRange;
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
            float clampedX = Mathf.Clamp(finalPos.x, center.x - size.x / 2f + 1f, center.x + size.x / 2f - 1f);
            float clampedY = Mathf.Clamp(finalPos.y, center.y - size.y / 2f + 1f, center.y + size.y / 2f - 1f);
            finalPos = new Vector2(clampedX, clampedY);
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
                
                bool isPathBlocked = false;
                if (_pathWaypoints != null && _pathWaypoints.Count > 0 && type != GoalType.Combat)
                {
                    Vector2 lastWp = _pathWaypoints[_pathWaypoints.Count - 1];
                    if (Vector2.Distance(lastWp, finalPos) > 1.5f)
                    {
                        isPathBlocked = true;
                    }
                }

                if (_pathWaypoints != null && _pathWaypoints.Count > 0 && !isPathBlocked)
                {
                    GoalPosition = _pathWaypoints[0]; // Điểm trung chuyển đầu tiên
                }
                else
                {
                    if (_pathWaypoints != null) _pathWaypoints.Clear(); // Xóa đường đi cụt

                    // LỖI TÌM ĐƯỜNG: Vị trí đích không thể đi tới (nằm trong đá/kẹt)
                    GoalPosition = botPos; // Dừng lại để lập tức chọn mục tiêu mới
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
