using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// TankAgent ULTRA — Bot Tank AI Chuyên Nghiệp
/// File duy nhất. Xóa TankAgent.cs, TankAgentPro.cs.
///
/// ═══════════════════════════════════════════════════════
/// TỔNG ĐẦU VÀO: 98 giá trị (Space Size trong Unity)
/// ═══════════════════════════════════════════════════════
/// [A] Quét Môi Trường (16 tia) = 32
/// [B] Tự quan sát (Self) = 6
/// [C] Waypoint = 3
/// [D] Dự đoán đạn (6 viên) = 36
/// [E] Trạng thái + Chiến lược = 21
///
/// [B] 32 Tia Định Hướng (30m) × 4 = 128
///     Khoảng cách | Loại vật thể | Hành lang Trái | Hành lang Phải
///
/// [C] Dự đoán Đạn: 6 viên gần nhất × 6 = 36
///     [pos_now | pos_0.3s | pos_0.6s] dưới tọa độ local bot
///
/// [D] Bộ nhớ 4 frames địch × 2 = 8 (tính vận tốc địch → chì đạn)
///
/// [E] Trạng thái + Chiến lược = 18
///     Bao gồm: góc ngắm chì đạn, LOS địch, đang cover, situation flags
///
/// HỆ THỐNG NGẮM BẮN (Lead Targeting):
///     t_bay = dist_to_enemy / bullet_speed
///     lead_pos = enemy_pos + enemy_velocity × t_bay
///     Quan sát: góc giữa nòng súng và lead_pos
///     Thưởng: bắn khi góc nhỏ → Bot học tự ngắm chì đạn
///
/// HỆ THỐNG CHIẾN LƯỢC (Emergent via Reward):
///     [Giao Tranh]  — khoảng cách lý tưởng + strafe + bắn đúng lúc
///     [Rút lui]     — máu thấp → tự di chuyển xa địch
///     [Tuần tra]    — thưởng di chuyển khi không thấy địch
///     [Nhặt đồ]     — thưởng tiến về phía coin/item nhìn thấy được
///     [Thoát hẹp]   — thưởng khi openness tăng
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BotMemorySystem))]
public class TankAgentUltra : Agent
{
    // ═══════════════════════════════════════════════════════════════
    //  REFERENCES
    // ═══════════════════════════════════════════════════════════════
    [Header("Liên kết xe tăng")]
    [SerializeField] private TankPlayer  tankPlayer;
    [SerializeField] private BoPhongDan  boPhongDan;
    [SerializeField] private Transform   turretTransform;
    [SerializeField] private Transform   diemSpawnDan;

    // ═══════════════════════════════════════════════════════════════
    //  LAYERS
    // ═══════════════════════════════════════════════════════════════
  

    [Header("Layer Masks")]
    [SerializeField] private LayerMask layerVatCan;    // Default
    [SerializeField] private LayerMask layerDich;      // Player
    [SerializeField] private LayerMask layerTaiNguyen; // Pickup
    [SerializeField] private LayerMask layerDan;       // VienDan

    // ─── Lịch sử Quyết định ──────────────────────────────────────────
    private System.Collections.Generic.HashSet<int> _daXetTaiNguyen = new System.Collections.Generic.HashSet<int>();
    private System.Collections.Generic.HashSet<int> _boQuaTaiNguyen = new System.Collections.Generic.HashSet<int>();

    // ═══════════════════════════════════════════════════════════════
    //  RADAR
    // ═══════════════════════════════════════════════════════════════
    private const int   V1 = 9;   private const float C1 = 0.7f;  private const int K1 = 5;
    private const int   V2 = 13;  private const float C2 = 1.5f;  private const int K2 = 5;
    private const int   V3 = 19;  private const float C3 = 2.0f;  private const int K3 = 4;
    private const int   V4 = 21;  private const float C4 = 3.0f;  private const int K4 = 3;

    private const int   SO_TIA      = 16;
    private const float TAM_TIA     = 30f;
    private const float TAM_HANH_LANG = 8f;
    private const int   MAX_DAN     = 6;
    private const float MEMORY_LEN_F = 4f;
    private const int   MEMORY_LEN  = 4;

    // ═══════════════════════════════════════════════════════════════
    //  COMBAT
    // ═══════════════════════════════════════════════════════════════
    [Header("Di chuyển & Chiến đấu")]
    [SerializeField] private float tocDoXe            = 5f;
    [SerializeField] private float tocDoXoay          = 180f;
    [SerializeField] private float tocDoXoayNong      = 150f;
    [SerializeField] private float khoangCachLyTuong  = 7f;
    [SerializeField] private float khoangCachToiDa    = 30f;
    [SerializeField] private float tocDoDan           = 12f;  // Tốc độ đạn để tính chì
    [SerializeField] private float cooldownBan        = 0.8f;

    // ═══════════════════════════════════════════════════════════════
    //  REWARDS
    // ═══════════════════════════════════════════════════════════════
#pragma warning disable 0414 // Tạm ẩn warning biến không sử dụng
    [Header("Phần thưởng")]
    [SerializeField] private float r_BanTrung         =  0.5f;
    [SerializeField] private float r_TieuDiet         =  2.0f;
    [SerializeField] private float r_NhatCoin         =  0.1f;
    [SerializeField] private float r_NhatItem         =  0.3f;
    [SerializeField] private float r_LeadShot         =  0.75f;  // Bắn trúng chì đạn
    [SerializeField] private float p_BiDanh           = -0.3f;
    [SerializeField] private float p_Chet             = -3.0f;
    [SerializeField] private float p_TongTuong        = -0.03f;
    [SerializeField] private float r_Strafe           =  0.015f;
    [SerializeField] private float r_Cover            =  0.025f;
    [SerializeField] private float r_Kiting          =  0.01f;
    [SerializeField] private float r_NeDan            =  0.1f;
    [SerializeField] private float r_TuanTra           =  0.05f;  // Di chuyển khi không có địch   // Tiến đến mục tiêu   // Đang quay vòng
#pragma warning restore 0414

    // ═══════════════════════════════════════════════════════════════
    //  STATE
    // ═══════════════════════════════════════════════════════════════    // ── Tham chiếu nội bộ ──
    private Rigidbody2D rb;
    private Mau            healthComp;
    private CoinWallet     wallet;
    private BotMemorySystem memory;
    private Transform bodyTransform; // Thêm tham chiếu đến bodyTransform để xoay đúng

    private int   _mauTruoc;
    private int   _coinTruoc;
    private float _timerBan;
    private bool  _danSapTrung;
    private bool  _dangChoHoiSinh;

    private Queue<Vector2>    _memDich     = new Queue<Vector2>();
    private TankPlayer[] _allPlayers => FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
    private TankPlayer        _dich;
    private List<Rigidbody2D> _danhSachDan = new List<Rigidbody2D>();
    private float             _currentGas;
    private float             _currentSteer;
    private float             _currentTurretRot;

    private float             _unstuckTimer;
    private float             _waypointDistTruoc;
    private bool              _isReversingGear = false;

    // ═══════════════════════════════════════════════════════════════
    //  0. HỒI SINH (3s Delay)
    // ═══════════════════════════════════════════════════════════════
    private System.Collections.IEnumerator XuLyChetVaHoiSinh()
    {
        _dangChoHoiSinh = true;
        // Tắt hiển thị bot, tắt va chạm để giả lập "chết"
        rb.simulated = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        
        yield return new WaitForSeconds(3f);
        
        // Bật lại
        rb.simulated = true;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = true;
        _dangChoHoiSinh = false;
        
        EndEpisode();
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. INITIALIZE
    // ═══════════════════════════════════════════════════════════════
    public override void Initialize()
    {
        rb     = GetComponent<Rigidbody2D>();
        memory = GetComponent<BotMemorySystem>();
        if (memory == null) memory = gameObject.AddComponent<BotMemorySystem>();
        
        // Thiết lập bộ nhớ vĩnh viễn (lưu trọn đời cho đến khi chết)
        memory.poiMaxAge = float.MaxValue;

        if (tankPlayer == null) tankPlayer = GetComponent<TankPlayer>();
        if (tankPlayer != null)
        {
            healthComp = tankPlayer.Health;
            wallet     = tankPlayer.Wallet;
            if (boPhongDan == null) boPhongDan = tankPlayer.GetComponent<BoPhongDan>();
            
            // Xoay nòng:
            if (turretTransform == null)
            {
                var ngam = tankPlayer.GetComponentInChildren<NguoiChoiNgamBan>();
                if (ngam != null) turretTransform = ngam.turretTransform; // Lấy đúng nòng súng
            
                // Fallback nếu người dùng đã gỡ script NguoiChoiNgamBan
                if (turretTransform == null)
                {
                    var pivot = transform.Find("TurretPivot");
                    if (pivot != null) turretTransform = pivot;
                }
            }
        }

        var diChuyen = GetComponent<DiChuyenNguoiChoi>();
        if (diChuyen != null) bodyTransform = diChuyen.bodyTransform;
        
        // Cứu cánh nếu Bot bị gỡ script DiChuyenNguoiChoi
        if (bodyTransform == null)
        {
            Transform foundBody = transform.Find("TankBody");
            if (foundBody != null) bodyTransform = foundBody;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. EPISODE BEGIN
    // ═══════════════════════════════════════════════════════════════
    public override void OnEpisodeBegin()
    {
        _mauTruoc      = healthComp != null ? healthComp.MauHienTai.Value : 0;
        if (healthComp != null) healthComp.ResetForTraining();

        transform.position = LayViTriSpawn();
        transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        rb.velocity        = Vector2.zero;
        rb.angularVelocity = 0f;

        _mauTruoc      = healthComp != null ? healthComp.MauHienTai.Value : 0;
        _coinTruoc     = wallet     != null ? wallet.TotalCoins.Value      : 0;
        _timerBan      = 0f;
        _danSapTrung   = false;
        _memDich.Clear();
        _waypointDistTruoc = float.MaxValue;

        _daXetTaiNguyen.Clear();
        _boQuaTaiNguyen.Clear();

        // ⚠️ XÓA TOÀN BỘ MEMORY KHI BOT CHẾT
        if (memory != null) memory.ResetMemory();
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. COLLECT OBSERVATIONS — 4230 giá trị
    // ═══════════════════════════════════════════════════════════════
    public override void CollectObservations(VectorSensor sensor)
    {
        if (_dangChoHoiSinh) 
        {
            for (int i = 0; i < 99; i++) sensor.AddObservation(0f);
            return;
        }

        Vector2 botPos = transform.position;

        // ✅ Cập nhật Memory (Xóa các POI cũ/gần) TRƯỚC KHI Quét môi trường
        if (memory != null)
        {
            memory.UpdateMemory(botPos);
        }

        // Sau đó Quét môi trường để ghi nhận lại các POI còn tồn tại
        _dich = TimDich();
        CapNhatDan();
        ScanVaGhiNhanPOI();

        if (memory != null)
        {
            float healthRatioNow = healthComp != null ? (float)healthComp.MauHienTai.Value / Mathf.Max(1, healthComp.MauToiDa) : 1f;
            bool losToEnemy = _dich != null && CoLOS(botPos, (Vector2)_dich.transform.position);
            
            if (_dich != null && losToEnemy) memory.GhiNhanDich(_dich.transform.position);
            
            memory.CapNhatMucTieu(botPos, healthRatioNow, _dich, losToEnemy);
        }

        // ── Thay đổi 1: Quét môi trường (32 obs) ──
        QuetMoiTruong(sensor, botPos);

        // ── Thay đổi 2: Tự quan sát bản thân (6 obs) ──
        sensor.AddObservation(_currentGas);
        sensor.AddObservation(_currentSteer);
        sensor.AddObservation(Mathf.Clamp(rb.velocity.x / tocDoXe, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.velocity.y / tocDoXe, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(_currentSteer, -1f, 1f));
        
        Vector2 currentUp = bodyTransform != null ? (Vector2)bodyTransform.up : (Vector2)transform.up;
        sensor.AddObservation(Vector2.Dot(rb.velocity.normalized, currentUp));

        // ── Thay đổi 3: Hệ thống Waypoint A* (3 obs) ──
        Vector2 goalPos = memory != null ? memory.GoalPosition : botPos;
        Vector2 toWaypoint   = goalPos - botPos;
        sensor.AddObservation(Mathf.Clamp(toWaypoint.x / 30f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(toWaypoint.y / 30f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp01(toWaypoint.magnitude / 30f));
        sensor.AddObservation(toWaypoint.normalized.y);

        // ── Dự đoán quỹ đạo đạn (36 obs) ──
        int cnt = 0;
        foreach (Rigidbody2D danRb in _danhSachDan)
        {
            if (cnt >= MAX_DAN) break;
            if (danRb == null) continue;

            Vector2 vel = danRb.velocity.magnitude > 0.1f
                ? danRb.velocity : (Vector2)danRb.transform.up * tocDoDan;

            for (int step = 0; step < 3; step++)
            {
                Vector2 futureWorld = danRb.position + vel * (step * 0.3f);
                Vector2 local       = transform.InverseTransformPoint(futureWorld);
                sensor.AddObservation(Mathf.Clamp(local.x / 15f, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(local.y / 15f, -1f, 1f));
            }
            cnt++;
        }
        for (int i = cnt; i < MAX_DAN; i++)
            for (int j = 0; j < 6; j++) sensor.AddObservation(0f);

        // ── Thông tin kẻ địch và trạng thái (21 obs) ──
        float mauMax = healthComp != null ? Mathf.Max(1, healthComp.MauToiDa)  : 1f;
        float mauHT  = healthComp != null ? healthComp.MauHienTai.Value         : 0f;
        sensor.AddObservation(mauHT / mauMax);                                   // 1

        float coin = wallet != null ? wallet.TotalCoins.Value : 0f;
        sensor.AddObservation(Mathf.Clamp01(coin / 100f));                       // 2

        float openness = DoOpenness();
        sensor.AddObservation(openness / 360f);                                  // 3

        if (_dich != null)
        {
            Vector2 ePosNow = _dich.transform.position;
            bool   losEnemy   = CoLOS(botPos, ePosNow);
            Vector2 toEnemy   = ePosNow - botPos;
            float  dist       = toEnemy.magnitude;

            // --- Cập nhật Memory cho Địch ---
            Vector2 ePosForMem = ePosNow;
            _memDich.Enqueue(ePosForMem);
            while (_memDich.Count > MEMORY_LEN) _memDich.Dequeue();

            Vector2[] memArr = new System.Collections.Generic.List<Vector2>(_memDich).ToArray();
            Vector2 velDich  = memArr.Length >= 2
                ? (memArr[memArr.Length - 1] - memArr[0]) / MEMORY_LEN_F
                : Vector2.zero;
            // ---------------------------------

            sensor.AddObservation(Mathf.Clamp01(dist / khoangCachToiDa));        // 4
            
            float enemyHealth = 1f;
            if (_dich.TryGetComponent<Mau>(out var enemyHp))
                enemyHealth = (float)enemyHp.MauHienTai.Value / Mathf.Max(1, enemyHp.MauToiDa);
            sensor.AddObservation(enemyHealth);                                  // 5

            sensor.AddObservation(Vector2.SignedAngle(currentUp, toEnemy.normalized) / 180f); // 6

            // Tính lead position thực sự
            float   tBay    = dist / Mathf.Max(0.1f, tocDoDan);
            Vector2 leadPos = ePosNow + velDich * tBay;
            if (!CoLOS(botPos, leadPos)) leadPos = ePosNow;

            if (turretTransform != null)
            {
                Vector2 toLead     = leadPos - botPos;
                sensor.AddObservation(Vector2.SignedAngle(turretTransform.up, toLead.normalized) / 180f); // 7
                sensor.AddObservation(Mathf.Clamp01(toLead.magnitude / khoangCachToiDa));                 // 8
            }
            else
            {
                sensor.AddObservation(0f); // 7
                sensor.AddObservation(0f); // 8
            }

            sensor.AddObservation(losEnemy ? 1f : 0f); // 9

            float faceAngle = Vector2.SignedAngle(_dich.transform.up, (botPos - ePosNow).normalized);
            sensor.AddObservation(faceAngle / 180f);   // 10

            // Thay 4 obs = 0f bằng velDich + situational flags
            sensor.AddObservation(Mathf.Clamp(velDich.x / tocDoXe, -1f, 1f)); // 11
            sensor.AddObservation(Mathf.Clamp(velDich.y / tocDoXe, -1f, 1f)); // 12
            sensor.AddObservation(dist < khoangCachLyTuong ? 1f : 0f);        // 13
            sensor.AddObservation(losEnemy && dist < 10f ? 1f : 0f);          // 14
            
            bool inCover = KiemTraCover(botPos, ePosNow, dist);
            sensor.AddObservation(inCover ? 1f : 0f);  // 15
        }
        else
        {
            sensor.AddObservation(1f); // 4
            for (int i = 0; i < 11; i++) sensor.AddObservation(0f); // 5-15
        }

        sensor.AddObservation(Mathf.Clamp01(_timerBan / cooldownBan)); // 19
        sensor.AddObservation(_currentSteer);              // 20
        int chiPhi = boPhongDan != null ? boPhongDan.GetChiPhiBan() : 1;
        sensor.AddObservation(wallet != null && wallet.TotalCoins.Value >= chiPhi ? 1f : 0f); // 21

        // Pad 3 giá trị cho đủ 99
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
    }

    private void QuetMoiTruong(VectorSensor sensor, Vector2 botPos)
    {
        Vector2 currentUp = bodyTransform != null ? (Vector2)bodyTransform.up : (Vector2)transform.up;
        for (int i = 0; i < 16; i++)
        {
            float   goc = i * (360f / 16f);
            Vector2 dir = Quaternion.Euler(0, 0, goc) * currentUp;

            RaycastHit2D hit = Physics2D.Raycast(botPos, dir, 30f,
                layerVatCan | layerDich | layerTaiNguyen | layerDan);

            sensor.AddObservation(hit.collider != null ? hit.distance / 30f : 1f);
            sensor.AddObservation(hit.collider != null ? PhanLoai(hit.collider) : 0f);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  4. ON ACTION RECEIVED
    //     Continuous[0] = Gas       : -1 lùi → +1 tiến
    //     Continuous[1] = Steer     : -1 trái → +1 phải (xoay THÂN)
    //     Continuous[2] = TurretRot : -1 trái → +1 phải (xoay NÒNG độc lập)
    //     Discrete[0]   = Fire      : 0 = không bắn, 1 = bắn
    // ═══════════════════════════════════════════════════════════════
    private void FixedUpdate()
    {
        if (_dangChoHoiSinh) return;

        _timerBan -= Time.fixedDeltaTime; // Giảm cooldown bắn liên tục

        Vector2 currentUp = bodyTransform != null ? (Vector2)bodyTransform.up : (Vector2)transform.up;

        // Tiến lùi dùng velocity để tương tác vật lý và đo tốc độ mượt mà
        rb.velocity = currentUp * _currentGas * tocDoXe;

        // Xoay hướng: Đối với cấu trúc xe tăng có body riêng
        float xoayGoc = -_currentSteer * tocDoXoay * Time.fixedDeltaTime;
        if (bodyTransform != null)
        {
            bodyTransform.Rotate(0f, 0f, xoayGoc);
        }
        else
        {
            rb.MoveRotation(rb.rotation + xoayGoc);
        }

        if (turretTransform != null)
        {
            if (_dich != null && boPhongDan != null && CoLOS(transform.position, _dich.transform.position))
            {
                Vector2 ePosNow = _dich.transform.position;
                Vector2 velDich = _dich.GetComponent<Rigidbody2D>().velocity;
                float dist = Vector2.Distance(transform.position, ePosNow);
                float tBay = dist / Mathf.Max(0.1f, tocDoDan);
                Vector2 leadPos = ePosNow + velDich * tBay;
                Vector2 toLead = leadPos - (Vector2)turretTransform.position;

                if (toLead.sqrMagnitude > 0.01f)
                {
                    // Ngắm mượt mà vào kẻ địch (Lerp 50f cũ quá nhanh khiến súng bị giật lắc)
                    turretTransform.up = Vector3.Slerp(turretTransform.up, toLead.normalized, Time.fixedDeltaTime * 15f);

                    // Ép tự động bắn ngay trong FixedUpdate (Đảm bảo bắn mượt mà không phụ thuộc OnActionReceived)
                    float angleToLead = Vector2.Angle(turretTransform.up, toLead);
                    if (angleToLead < 15f && _timerBan <= 0f) // Góc nhỏ hơn 15 độ là khai hỏa
                    {
                        // ── 3. NGẮM BẮN THÔNG MINH (Smart Raycast Aiming) ──
                        // Bắn tia Raycast ảo xem đạn có bị kẹt tường không
                        RaycastHit2D[] hits = Physics2D.RaycastAll(turretTransform.position, toLead.normalized, toLead.magnitude, layerVatCan);
                        bool isBlocked = false;
                        foreach (var h in hits)
                        {
                            if (h.collider != null)
                            {
                                if (h.collider.isTrigger) continue; // Bỏ qua vật phẩm (Coins, Máu...)
                                
                                // Kẻ địch hoặc bản thân có thể có collider nằm ở object con (Child)
                                TankPlayer hitPlayer = h.collider.GetComponentInParent<TankPlayer>();
                                if (hitPlayer == null || (hitPlayer.gameObject != gameObject && hitPlayer.gameObject != _dich.gameObject))
                                {
                                    isBlocked = true;
                                    break;
                                }
                            }
                        }
                        if (!isBlocked)
                        {
                            boPhongDan.BotRequestFire();
                            _timerBan = cooldownBan;
                        }
                    }
                }
                else if (_timerBan <= 0f)
                {
                    // Đứng quá sát nhau (chồng lên nhau), nhắm mắt bắn bừa luôn
                    boPhongDan.BotRequestFire();
                    _timerBan = cooldownBan;
                }
            }
            else
            {
                turretTransform.Rotate(0f, 0f, -_currentTurretRot * tocDoXoayNong * Time.fixedDeltaTime);
            }
        }

        if (KiemTraDanGanTrung())
        {
            RequestDecision();
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_dangChoHoiSinh) return;

        float gas       = actions.ContinuousActions[0];
        float steer     = actions.ContinuousActions[1];
        
        // [AUTO-PILOT HACK] Bỏ qua AI chưa train, ép Bot tự lái cứng bám theo đường màu Tím (A*)
        Vector2 toGoal = Vector2.zero;
        float originalDist = 0f;

        // 1. ĐỊNH HƯỚNG CHIẾN LƯỢC TỪ BỘ NHỚ (Đường A*, Giao Tranh, Rút Lui)
        if (memory != null)
        {
            toGoal = memory.GoalPosition - (Vector2)transform.position;
            originalDist = toGoal.magnitude;

            // [FIXED] Dừng lao thẳng vào mặt kẻ địch, giữ cự ly 3m để bắn, đồng thời đi ngang (strafe) để né đạn và tránh vón cục
            if (memory.CurrentGoal == BotMemorySystem.GoalType.Combat && originalDist < 3.0f)
            {
                toGoal = new Vector2(-toGoal.y, toGoal.x) * 0.5f; // Đảo vector để lách ngang
            }

            // Chuẩn hóa toGoal thành vector đơn vị để lực Tách bầy (Flocking) không bị vô hiệu hóa khi mục tiêu quá xa
            if (toGoal.magnitude > 0.01f)
            {
                toGoal = toGoal.normalized;
            }
            
            // ── VÔ THỨC NHẶT ĐỒ (Unconscious Looting) ──
            // Chỉ bị lực hút vô thức khi đang đi Khám phá (Explore), tránh cộng dồn vector khi đang trực tiếp nhặt đồ (Loot)
            if (memory.CurrentGoal == BotMemorySystem.GoalType.Explore)
            {
                var nearbyItem = memory.TimPOIGanNhat(memory.GetItemPOIs(), transform.position);
                if (nearbyItem == null) nearbyItem = memory.TimPOIGanNhat(memory.GetCoinPOIs(), transform.position); // Ưu tiên Súng > Tiền
                
                if (nearbyItem != null && Vector2.Distance(transform.position, nearbyItem.Value.worldPos) < 5f)
                {
                    // Trộn vector đích đến chính và vector "lực hút" của item
                    Vector2 pullDir = (nearbyItem.Value.worldPos - (Vector2)transform.position).normalized;
                    toGoal = toGoal.normalized * 0.8f + pullDir * 1.5f; // Trộn lực hút mạnh
                }
            }

        }

        // 2. PHẢN XẠ NÉ ĐẠN THẦN THÁNH (Vừa né đạn vừa chạy theo mục tiêu chiến lược)
        if (_danhSachDan != null)
        {
            foreach (var danRb in _danhSachDan)
            {
                if (danRb == null) continue;
                Vector2 vel = danRb.velocity.magnitude > 0.1f ? danRb.velocity : (Vector2)danRb.transform.up * tocDoDan;
                Vector2 futur = danRb.position + vel * 0.4f; // Dự đoán 0.4 giây tới
                if (Vector2.Distance(futur, transform.position) < 1.5f)
                {
                    // Lách sang ngang (Vuông góc với đường đạn)// Lách sang ngang (Vuông góc với đường đạn)
                    Vector2 danDir = vel.normalized;
                    Vector2 dodgeDir = new Vector2(-danDir.y, danDir.x);

                    // Chọn hướng lách thuận chiều mũi xe nhất
                    if (Vector2.Dot((bodyTransform != null ? bodyTransform.up : transform.up), dodgeDir) < 0)
                        dodgeDir = -dodgeDir;

                    // CỘNG GỘP VECTOR: Vừa di chuyển theo chiến lược (rút lui/tấn công), vừa né đạn!
                    toGoal = toGoal.normalized + dodgeDir * 2f;
                    break;
                }
            }
        }


        // 4. THUẬT TOÁN TÁCH BẦY VÀ TRƯỢT TƯỜNG (Tránh vật cản linh hoạt khi đã ở quá gần)
        Collider2D[] nearbyObstacles = Physics2D.OverlapCircleAll(transform.position, 2.0f, layerVatCan | layerDich);
            foreach (var col in nearbyObstacles)
            {
                if (col.isTrigger || col.transform.root == transform.root) continue;
                
                bool isWall = ((1 << col.gameObject.layer) & layerVatCan) != 0;
                float repelRadius = isWall ? 1.2f : 2.5f; // [FIXED] Tăng bán kính vách tường lên 1.2m để luôn đi giữa hẻm
                float forceMult = isWall ? 8.0f : 4.0f; // [FIXED] Lực của Tường là 8.0f (Kháng lại lực 4.0f của xe tăng khác, ép xe không bao giờ bị dồn vào tường)

                Vector2 away;
                float dist;

                if (!isWall)
                {
                    // LỖI DÍNH LẸO: Nếu dùng ClosestPoint khi 2 xe đè lên nhau, nó trả về 0 -> Mất lực tách bầy!
                    // Fix: Đối với xe khác, luôn dùng khoảng cách giữa 2 TÂM XE để ép đẩy nhau ra
                    away = (Vector2)transform.position - (Vector2)col.transform.position;
                    dist = away.magnitude;
                    
                    // Nếu xui xẻo trùng tâm 100%, random lực đẩy ra
                    if (dist < 0.01f)
                    {
                        away = UnityEngine.Random.insideUnitCircle;
                        dist = 0.01f;
                    }
                }
                else
                {
                    // Đối với tường thì vẫn lấy điểm gần nhất trên bề mặt
                    Vector2 closestPoint = col.ClosestPoint(transform.position);
                    away = (Vector2)transform.position - closestPoint;
                    dist = away.magnitude;
                }

                if (dist < repelRadius)
                {
                    if (isWall && dist > 0.01f)
                    {
                        // TRƯỢT TƯỜNG LINH HOẠT (Wall Sliding / Tangent Avoidance)
                        // Lấy 2 vector tiếp tuyến (song song với mặt tường)
                        Vector2 tangent1 = new Vector2(-away.y, away.x).normalized;
                        Vector2 tangent2 = new Vector2(away.y, -away.x).normalized;
                        
                        // Chọn hướng trượt thuận với đường đi tới mục tiêu nhất
                        Vector2 bestTangent = Vector2.Dot(tangent1, toGoal.normalized) > Vector2.Dot(tangent2, toGoal.normalized) ? tangent1 : tangent2;
                        
                        // Vector tổng hợp = Vừa đẩy nhẹ ra xa để không cạ (30%) + Vừa trượt mượt mà dọc theo tường (70%)
                        Vector2 slideDir = (away.normalized * 0.3f + bestTangent * 0.7f).normalized;
                        toGoal += slideDir * (repelRadius - dist) * forceMult;
                    }
                    else if (!isWall)
                    {
                        // [FIXED] TRƯỢT NGANG (Tangent Avoidance) thay vì đẩy lùi 180 độ
                        Vector2 tangent1 = new Vector2(-away.y, away.x).normalized;
                        Vector2 tangent2 = new Vector2(away.y, -away.x).normalized;
                        
                        // Chọn hướng lách sang ngang thuận với đường đi tới mục tiêu nhất
                        Vector2 bestTangent = Vector2.Dot(tangent1, toGoal.normalized) > Vector2.Dot(tangent2, toGoal.normalized) ? tangent1 : tangent2;
                        
                        // Kết hợp 40% lực đẩy nhẹ ra xa và 60% lực lách sang ngang để hai xe "trượt" qua nhau êm ái
                        Vector2 dodgeDir = (away.normalized * 0.4f + bestTangent * 0.6f).normalized;
                        toGoal += dodgeDir * (repelRadius - dist) * forceMult;
                    }
                }
            }

        if (memory != null && memory.IsLooping)
        {
            _unstuckTimer = 1.5f; // Bắt đầu chuỗi gỡ kẹt: 1s xoay + 0.5s vọt
        }

        if (_unstuckTimer > 0f)
        {
            _unstuckTimer -= Time.fixedDeltaTime;
            if (_unstuckTimer > 0.5f)
            {
                // Giai đoạn 1: Vừa lùi vừa bẻ lái (J-turn) để thoát khỏi góc kẹt hiệu quả hơn thay vì lùi thẳng
                gas = -1f;
                steer = 1f;
            }
            else
            {
                // Giai đoạn 2: Phóng lên kèm bẻ lái ngược lại để vọt ra khỏi vùng kẹt
                gas = 1f;
                steer = -1f;
            }
        }
        else
        {
            Vector2 currentUp = bodyTransform != null ? (Vector2)bodyTransform.up : (Vector2)transform.up;
            float angle = Vector2.SignedAngle(currentUp, toGoal);

            if (originalDist < 0.1f)
            {
                // Nếu đích đến trùng với vị trí hiện tại (vector 0), giữ nguyên hướng hiện tại, không được ép xoay 90 độ gây giật xe!
                toGoal = currentUp;
                angle = 0f;
            }

            if (memory != null && memory.CurrentGoal == BotMemorySystem.GoalType.Heal && originalDist < 1.0f)
            {
                // Đã đến gần trạm hồi máu -> Phanh xe đứng yên để hồi máu
                gas = 0f;
                steer = 0f;
            }
            else
            {
                // ── LÁI NHẠY VÀ LINH HOẠT HƠN (Arcade Tank Steering & Hysteresis Reverse) ──
                float dot = Vector2.Dot(currentUp, toGoal.normalized);
                
                // Hysteresis: Hạ ngưỡng lùi xuống -0.9f để xe ưu tiên ĐÁNH LÁI thay vì GIẬT SỐ LÙI mỗi khi gặp cua rẽ gắt
                float reverseThreshold = _isReversingGear ? -0.4f : -0.9f; 

                if (dot < reverseThreshold)
                {
                    _isReversingGear = true;
                    // Mục tiêu ở đằng sau -> Chạy lùi linh hoạt thay vì xoay compa
                    gas = -1f;
                    
                    // Tính góc sai lệch so với phía sau đuôi xe (180 độ)
                    float backAngle = angle > 0 ? 180f - angle : -180f - angle; 
                    steer = backAngle / 25f; // Lái mượt mà để đuôi xe chĩa vào mục tiêu
                    steer = Mathf.Clamp(steer, -1f, 1f);
                }
                else
                {
                    _isReversingGear = false;
                    // Mục tiêu ở phía trước hoặc ngang hông -> Chạy tới
                    gas = Mathf.Lerp(0.4f, 1.0f, (dot + 1f) / 2f); // Ga nhạy theo góc cua
                    
                    // [CẬP NHẬT] Chống quay compa (Orbiting) và vượt lố (Overshoot) khi mục tiêu ở quá gần
                    if (originalDist < 1.5f)
                    {
                        // 1. Phanh theo góc (Tránh quay compa)
                        if (Mathf.Abs(angle) > 30f)
                        {
                            float anglePenalty = Mathf.Clamp01(1f - ((Mathf.Abs(angle) - 30f) / 60f)); 
                            gas *= anglePenalty;
                        }
                        
                        // 2. Phanh theo khoảng cách (Tránh vượt lố đà)
                        // Nếu mục tiêu đang ở ngay dưới chân (< 0.8m), giảm tốc độ rà phanh từ từ
                        if (originalDist < 0.8f)
                        {
                            gas *= Mathf.Max(0.2f, originalDist / 0.8f); // Tối thiểu chạy 20% ga để lết tới cắn item
                        }
                    }

                    steer = -angle / 25f; // Xoay cực nhạy
                    steer = Mathf.Clamp(steer, -1f, 1f);
                }
            }
        }

        // Truyền trực tiếp giá trị Analog vào bộ điều khiển xe, KHÔNG ép cứng về -1, 0, 1 nữa để tránh giật xe
        _currentGas = gas;
        _currentSteer = steer;

        _currentTurretRot = actions.ContinuousActions[2];
        int   fire        = actions.DiscreteActions[0];

        // ── Ghi nhận quyết định bắn của mạng nơ ron để tính Reward ─────────
        bool wantToShoot = fire == 1;

        if (wantToShoot && boPhongDan != null)
        {
            AddReward(0.01f); // Khuyến khích bắn nếu AI tự quyết định
        }

        // ═══ REWARD SHAPING ═════════════════════════════════════
        float dt    = Time.fixedDeltaTime;
        float speed = rb.velocity.magnitude;

        // ── 1. REWARD DI CHUYỂN (dense, mỗi frame) ──────────────────
        // Thưởng tốc độ — bot LUÔN muốn chạy
        AddReward(speed / tocDoXe * 0.005f);

        // Phạt đứng yên ngay lập tức
        if (speed < 0.3f) AddReward(-0.004f);

        // ── 2. REWARD XOAY HỢP LÝ ───────────────────────────────────
        // Thưởng xoay khi đang chạy (học rằng xoay + chạy = hữu ích)
        if (Mathf.Abs(_currentSteer) > 0.3f && speed > 1f)
            AddReward(0.003f);

        // Phạt xoay tại chỗ (spinning vô ích)
        if (Mathf.Abs(_currentSteer) > 0.5f && speed < 0.5f)
            AddReward(-0.005f);

        // ── 3. WAYPOINT REWARD (Dẫn đường bằng A*) ────────────
        if (memory != null)
        {
            float waypointDist = Vector2.Distance(transform.position, memory.GoalPosition);

            if (_waypointDistTruoc < float.MaxValue)
            {
                float progress = _waypointDistTruoc - waypointDist;
                // Thưởng khi bám theo node A*
                AddReward(progress * 0.15f);
            }
            _waypointDistTruoc = waypointDist;
        }

        // ── 4. CHIẾN THUẬT (chỉ khi có địch) ────────────────────────
        // Phân tích môi trường hẹp (Corridor detection)
        RaycastHit2D leftWall = Physics2D.Raycast(transform.position, -transform.right, 4f, layerVatCan);
        RaycastHit2D rightWall = Physics2D.Raycast(transform.position, transform.right, 4f, layerVatCan);
        bool isNarrowCorridor = (leftWall.collider != null && leftWall.distance < 2.5f) && 
                                (rightWall.collider != null && rightWall.distance < 2.5f);

        if (_dich != null)
        {
            Vector2 toEnemy = (Vector2)_dich.transform.position - (Vector2)transform.position;
            float   dist    = toEnemy.magnitude;

            // Kiting — duy trì khoảng cách lý tưởng
            if (dist >= khoangCachLyTuong - 1.5f && dist <= khoangCachLyTuong + 2.5f)
                AddReward(r_Kiting * dt);

            // Circle Strafing — đi ngang (Chỉ kích hoạt khi ở không gian mở)
            if (!isNarrowCorridor && rb.velocity.magnitude > 2f)
            {
                float dot = Mathf.Abs(Vector2.Dot(rb.velocity.normalized, toEnemy.normalized));
                if (dot < 0.35f) AddReward(r_Strafe * dt);
            }
            // Trong hành lang hẹp, thưởng khi lùi lại an toàn thay vì strafe
            else if (isNarrowCorridor)
            {
                float dot = Vector2.Dot(rb.velocity.normalized, toEnemy.normalized);
                if (dot < -0.5f) AddReward(r_Cover * dt); // lùi lại
            }

            // Truy sát khi địch máu thấp
            float myHp = healthComp != null ? (float)healthComp.MauHienTai.Value / Mathf.Max(1, healthComp.MauToiDa) : 1f;
            float enemyHpRatio = 1f;
            if (_dich.TryGetComponent<Mau>(out var eHp))
                enemyHpRatio = (float)eHp.MauHienTai.Value / Mathf.Max(1, eHp.MauToiDa);
            if (enemyHpRatio < 0.3f && myHp > 0.3f
                && Vector2.Dot(rb.velocity.normalized, toEnemy.normalized) > 0.5f)
                AddReward(0.04f * dt);
        }

        // ── 5. NÉ ĐẠN ────────────────────────────────────────────────
        bool danNow = KiemTraDanGanTrung();
        if (_danSapTrung && !danNow) AddReward(r_NeDan);
        _danSapTrung = danNow;

        // ── 5.5 THU THẬP TÀI NGUYÊN (DENSE REWARD) ───────────────────
        Collider2D closestItem = null;
        float minItemDist = 15f;
        Collider2D[] items = Physics2D.OverlapCircleAll(transform.position, 15f, layerTaiNguyen);
        foreach (var item in items)
        {
            if (!KiemTraGhiNhanTaiNguyen(item)) continue;

            float dist = Vector2.Distance(transform.position, item.transform.position);
            if (dist < minItemDist && CoLOS(transform.position, item.transform.position))
            {
                minItemDist = dist;
                closestItem = item;
            }
        }
        
        if (closestItem != null)
        {
            Vector2 toItem = (Vector2)closestItem.transform.position - (Vector2)transform.position;
            float dot = Vector2.Dot(rb.velocity.normalized, toItem.normalized);
            if (dot > 0.6f && speed > 1f) AddReward(0.01f * dt); // Thưởng khi chủ động lao tới item
        }

        // ── 6. HP + COIN (giữ nguyên) ────────────────────────────────
        if (healthComp != null)
        {
            int mauHT = healthComp.MauHienTai.Value;
            int delta  = mauHT - _mauTruoc;
            if (delta < 0) AddReward(p_BiDanh * Mathf.Abs(delta) * 0.1f);
            _mauTruoc = mauHT;
            if (mauHT <= 0 && !_dangChoHoiSinh)
            {
                AddReward(p_Chet);
                StartCoroutine(XuLyChetVaHoiSinh());
                return;
            }
        }
        if (wallet != null)
        {
            int coinHT = wallet.TotalCoins.Value;
            if (coinHT - _coinTruoc > 0) AddReward(r_NhatCoin * (coinHT - _coinTruoc) * 0.1f);
            _coinTruoc = coinHT;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  6. HEURISTIC — Test bằng người thật
    // ═══════════════════════════════════════════════════════════════
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        var d = actionsOut.DiscreteActions;
        c[0] = Input.GetAxis("Vertical");
        c[1] = Input.GetAxis("Horizontal");
        c[2] = Input.GetKey(KeyCode.Q) ? -1f : Input.GetKey(KeyCode.E) ? 1f : 0f;
        d[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  7. COLLISION & TRIGGER (XỬ LÝ NHẶT ĐỒ / ĐỤNG TƯỜNG)
    // ═══════════════════════════════════════════════════════════════
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & layerVatCan) != 0)
            AddReward(p_TongTuong);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // Nhặt được đồ / coin → Thưởng nóng ngay lập tức
        if (((1 << col.gameObject.layer) & layerTaiNguyen.value) != 0)
        {
            if (KiemTraGhiNhanTaiNguyen(col))
            {
                ItemPickup item = col.GetComponent<ItemPickup>();
                if (item != null && item.Type == ItemType.Trap)
                {
                    // Vô tình giẫm phải bẫy -> Phạt nặng để Bot học cách né (dù đã lơ bẫy trong tầm nhìn)
                    AddReward(-2.0f); 
                }
                else
                {
                    AddReward(r_NhatItem);
                }
            }
            else
            {
                // Nếu đã quyết định bỏ qua (vd Bẫy) nhưng vẫn đụng trúng -> Phạt
                ItemPickup item = col.GetComponent<ItemPickup>();
                if (item != null && item.Type == ItemType.Trap)
                {
                    AddReward(-2.0f);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE — SCAN & GHI NHẬN POI VÀO MEMORY
    // ═══════════════════════════════════════════════════════════════

    /// Quét vật thể xung quanh trong tầm nhìn và ghi vào BotMemorySystem.
    /// Gọi mỗi frame để memory luôn được cập nhật.
    private void ScanVaGhiNhanPOI()
    {
        if (memory == null) return;
        Vector2 botPos = transform.position;

        // Quét tài nguyên (coin) trong vòng 15m dựa trên layerTaiNguyen
        Collider2D[] hits = Physics2D.OverlapCircleAll(botPos, 15f, layerTaiNguyen);
        foreach (var col in hits)
        {
            int id = col.gameObject.GetInstanceID();
            if (_boQuaTaiNguyen.Contains(id)) continue;
            if (!_daXetTaiNguyen.Contains(id)) _daXetTaiNguyen.Add(id);

            Vector2 poiPos = col.transform.position;
            if (!CoLOS(botPos, poiPos)) continue;

            if (col.TryGetComponent<ItemPickup>(out _))
            {
                // Xử lý riêng biệt ở dưới
            }
            else if (col.TryGetComponent<HealingZone>(out var healZone))
            {
                if (healZone.CurrentHealPower > 0) memory.GhiNhanHealStation(poiPos);
            }
            else
            {
                memory.GhiNhanCoin(poiPos);
            }
        }

        // [FIX] Quét TOÀN BỘ ItemPickup bất kể Layer nào (Để chống lỗi cấu hình sai Layer trong Unity)
        // Đồng thời áp dụng hàm CanBePickedUpByBot để khôn ngoan né Bẫy (Traps)
        ItemPickup[] allItems = FindObjectsOfType<ItemPickup>();
        foreach (var item in allItems)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            int id = item.gameObject.GetInstanceID();

            if (_boQuaTaiNguyen.Contains(id)) continue;
            
            if (!_daXetTaiNguyen.Contains(id))
            {
                _daXetTaiNguyen.Add(id);
                // Gọi hàm logic gốc của Item để né bẫy và ép tỉ lệ nhặt 100%
                if (!item.CanBePickedUpByBot(null, 1.0f))
                {
                    _boQuaTaiNguyen.Add(id);
                    continue;
                }
            }

            Vector2 poiPos = item.transform.position;
            if (Vector2.Distance(botPos, poiPos) > 15f) continue; // Chỉ quan tâm item trong tầm nhìn 15m
            if (!CoLOS(botPos, poiPos)) continue; // Bị tường che thì thôi

            memory.GhiNhanItem(poiPos);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE — UTILITIES
    // ═══════════════════════════════════════════════════════════════

    /// Tìm một vị trí gần đây (bán kính 6m) có thể nấp khỏi tầm nhìn của địch
    private Vector2 FindCoverPosition(Vector2 currentPos, Vector2 enemyPos)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            Vector2 samplePos = currentPos + dir * 3f; // Bán kính nấp 3m xung quanh vị trí hiện tại
            
            // 1. Chỗ núp phải đi tới được (không bị tường chặn ngay trước mặt)
            if (!Physics2D.Linecast(currentPos, samplePos, layerVatCan))
            {
                // 2. Chỗ núp PHẢI BỊ CHE KHUẤT khỏi địch (Linecast chạm tường)
                if (Physics2D.Linecast(samplePos, enemyPos, layerVatCan))
                {
                    return samplePos; 
                }
            }
        }
        return currentPos; // Không tìm được chỗ nấp
    }

    /// Xử lý tỉ lệ quyết định nhặt item/coin
    private bool KiemTraGhiNhanTaiNguyen(Collider2D col)
    {
        int id = col.gameObject.GetInstanceID();
        if (_boQuaTaiNguyen.Contains(id)) return false;
        
        if (!_daXetTaiNguyen.Contains(id))
        {
            _daXetTaiNguyen.Add(id);

            ItemPickup item = col.GetComponent<ItemPickup>();
            if (item != null)
            {
                // Dùng chung logic thông minh (né bẫy) với BotBrain thường
                // CHÚ Ý: Đã ép tỉ lệ nhặt (tham số thứ 2) thành 1.0f
                if (!item.CanBePickedUpByBot(null, 1.0f))
                {
                    _boQuaTaiNguyen.Add(id);
                    return false;
                }
            }
            // Nếu là Coin bình thường thì auto 100% nhặt
        }
        return true;
    }

    private bool CoLOS(Vector2 from, Vector2 to)
    {
        Vector2 dir  = to - from;
        float   dist = dir.magnitude;
        if (dist < 0.05f) return true;
        
        LayerMask mask = GridManager.Instance != null ? GridManager.Instance.unwalkableMask : layerVatCan;
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to, mask);
        
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            // Bỏ qua nếu tia đụng trúng chính Bot hoặc xe tăng khác (xe tăng không nên che khuất tầm nhìn nhặt đồ)
            if (hit.collider.transform.root == transform.root || hit.collider.GetComponentInParent<TankPlayer>() != null)
                continue;
            // Bỏ qua nếu tia đụng trúng chính Item/Coin (phòng trường hợp user set sai Layer)
            if (hit.collider.GetComponentInParent<ItemPickup>() != null || hit.collider.GetComponentInParent<Coin>() != null)
                continue;

            // Nếu đụng bất cứ thứ gì khác (Tường, đá), thì tức là bị che
            return false;
        }
        
        return true;
    }

    /// Tính độ thông thoáng (0=bị vây, 1=hoàn toàn trống) bằng 8 tia ngắn
    private float TinhOpennessCua(Vector2 pos, float radius)
    {
        int thoang = 0;
        for (int i = 0; i < 8; i++)
        {
            float   goc = i * 45f;
            Vector2 dir = new Vector2(Mathf.Cos(goc * Mathf.Deg2Rad), Mathf.Sin(goc * Mathf.Deg2Rad));
            if (!Physics2D.Raycast(pos, dir, radius * 2f, layerVatCan)) thoang++;
        }
        return (float)thoang / 8f;
    }

    /// Tổng "góc trời" không bị tường chắn quanh bot (dùng 36 tia)
    private float DoOpenness()
    {
        float tong = 0f;
        const int n = 36;
        for (int i = 0; i < n; i++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, i * (360f / n)) * (Vector2)transform.up;
            if (!Physics2D.Raycast(transform.position, dir, 3f, layerVatCan))
                tong += 360f / n;
        }
        return tong;
    }

    /// Kiểm tra đang ẩn sau cover (có tường nửa đường + LOS đến địch)
    private bool KiemTraCover(Vector2 from, Vector2 enemyPos, float dist)
    {
        Vector2 dir = (enemyPos - from).normalized;
        bool wallNear = Physics2D.Raycast(from, dir, dist * 0.5f, layerVatCan);
        bool enemyBlocked = Physics2D.Raycast(enemyPos, -dir, dist * 0.9f, layerVatCan);
        return wallNear || enemyBlocked;
    }

    /// Phát hiện đạn đang nhắm vào bot trong ~0.5s
    private bool KiemTraDanGanTrung()
    {
        foreach (var danRb in _danhSachDan)
        {
            if (danRb == null) continue;
            Vector2 vel   = danRb.velocity.magnitude > 0.1f ? danRb.velocity : (Vector2)danRb.transform.up * tocDoDan;
            Vector2 futur = danRb.position + vel * 0.5f;
            if (Vector2.Distance(futur, transform.position) < 1.5f) return true;
        }
        return false;
    }

    /// Cập nhật danh sách đạn gần nhất
    private void CapNhatDan()
    {
        _danhSachDan.Clear();
        var hits = Physics2D.OverlapCircleAll(transform.position, 20f, layerDan);
        foreach (var col in hits)
            if (col.TryGetComponent<Rigidbody2D>(out var danRb))
                _danhSachDan.Add(danRb);

        _danhSachDan.Sort((a, b) =>
            Vector2.Distance(a.position, transform.position)
                .CompareTo(Vector2.Distance(b.position, transform.position)));
    }

    private float PhanLoai(Collider2D col)
    {
        int layer = col.gameObject.layer;
        if (((1 << layer) & layerVatCan.value)    != 0) return 0.25f;
        if (((1 << layer) & layerDich.value)       != 0) return 0.5f;
        if (((1 << layer) & layerTaiNguyen.value)  != 0)
        {
            if (KiemTraGhiNhanTaiNguyen(col)) return 0.75f;
            return 0f; // Nếu từ chối nhặt, coi như vô hình
        }
        if (((1 << layer) & layerDan.value)        != 0) return 1.0f;
        return 0f;
    }

    // ── 1. HỆ THỐNG SĂN MỒI & KS MẠNG (Target Selection Matrix) ──
    private TankPlayer TimDich()
    {
        TankPlayer best = null;
        float minScore  = float.MaxValue; // Điểm càng thấp càng ưu tiên
        if (_allPlayers == null) return null;

        foreach (var p in _allPlayers)
        {
            if (p == null || p == tankPlayer) continue;
            if (!p.gameObject.activeInHierarchy) continue;
            if (!p.TryGetComponent<Mau>(out var mau) || mau.MauHienTai.Value <= 0) continue;

            float dist = Vector2.Distance(transform.position, p.transform.position);
            
            // Khởi điểm: Điểm = Khoảng cách
            float score = dist;

            // Yếu tố 1: KS Mạng (Kill Steal). Kẻ địch máu càng thấp, điểm càng bị giảm (trở nên ưu tiên hơn).
            float healthRatio = (float)mau.MauHienTai.Value / Mathf.Max(1, mau.MauToiDa);
            float missingHPRatio = 1f - healthRatio;
            score -= missingHPRatio * 25f; // Giảm tối đa 25 điểm nếu địch chỉ còn 1 giọt máu

            // Yếu tố 2: Tầm nhìn (Line of Sight). Nếu bị che khuất bởi tường, cộng thêm điểm rủi ro.
            bool hasLOS = CoLOS(transform.position, p.transform.position);
            if (!hasLOS)
            {
                score += 15f; // Cộng 15 điểm phạt nếu địch nấp sau tường
            }

            if (score < minScore) 
            { 
                minScore = score; 
                best = p; 
            }
        }
        return best;
    }
    // [SYNC] God-tier AI update synced to IDE

    private Vector2 LayViTriSpawn()
    {
        var spawns = FindObjectsOfType<SpawnPoint>();
        return (spawns != null && spawns.Length > 0)
            ? (Vector2)spawns[Random.Range(0, spawns.Length)].transform.position
            : Random.insideUnitCircle * 12f;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GIZMOS
    // ═══════════════════════════════════════════════════════════════
    private void OnDrawGizmosSelected()
    {
        Vector2 pos = transform.position;

        // 4 vòng tầm nhìn
        Gizmos.color = new Color(1, 0,    0,    0.3f); Gizmos.DrawWireSphere(pos, V1 / 2f * C1);
        Gizmos.color = new Color(1, 0.5f, 0,    0.2f); Gizmos.DrawWireSphere(pos, V2 / 2f * C2);
        Gizmos.color = new Color(1, 1,    0,    0.1f); Gizmos.DrawWireSphere(pos, V3 / 2f * C3);
        Gizmos.color = new Color(0, 1,    0,    0.1f); Gizmos.DrawWireSphere(pos, V4 / 2f * C4);

        // 32 tia định hướng
        Gizmos.color = new Color(0, 0.8f, 1f, 0.4f);
        for (int i = 0; i < SO_TIA; i++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, i * (360f / SO_TIA)) * (Vector2)transform.up;
            Gizmos.DrawRay(pos, dir * TAM_TIA);
        }

        // LOS check preview — vùng mù sau tường
        Gizmos.color = new Color(1, 0, 0, 0.15f);
        int bk = V2 / 2;
        for (int r = -bk; r <= bk; r++)
        for (int c = -bk; c <= bk; c++)
        {
            Vector2 oPos = pos + ((Vector2)transform.right * c + (Vector2)transform.up * r) * C2;
            if (!CoLOS(pos, oPos))
                Gizmos.DrawCube(oPos, Vector3.one * C2 * 0.9f);
        }

        // Đường đạn dự đoán
        Gizmos.color = Color.red;
        foreach (var danRb in _danhSachDan)
        {
            if (danRb == null) continue;
            Vector2 v = danRb.velocity.magnitude > 0.1f ? danRb.velocity : (Vector2)danRb.transform.up * tocDoDan;
            Gizmos.DrawLine(danRb.position, danRb.position + v * 0.6f);
        }
    }


}

// [SYNC] Fixed trigger raycast bug IDE
