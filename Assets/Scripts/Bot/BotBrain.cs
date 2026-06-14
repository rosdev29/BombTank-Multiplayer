using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TankPlayer))]
[RequireComponent(typeof(BotSense))]
public class BotBrain : NetworkBehaviour
{
    [Header("Chu kỳ đánh giá")]
    [SerializeField] private float chuKyDanhGiaMin = 0.2f;
    [SerializeField] private float chuKyDanhGiaMax = 0.5f;

    [Header("Ngưỡng chuyển trạng thái")]
    // nguongMauThapDeRutLui đã XÓA — đọc từ ctx.Config.nguongRutLui
    [SerializeField] private float banKinhGiaoTranh = 10f;
    [SerializeField] private int   chiPhiBan        = 1;

    [Header("Config độ khó (fallback dev)")]
    [Tooltip("Kéo BotConfig_Medium vào đây. GanConfig() tự gọi trong OnNetworkSpawn nếu BotSpawner chưa wire.")]
    [SerializeField] private BotConfig configMacDinh;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    [Header("Né tường")]
    [SerializeField] private LayerMask layerMaskTuong;
    [SerializeField] private float khoangNeTuong = 2.5f;
    [SerializeField] private float khoangKhanCap = 0.6f;

    [Header("Anti-Stuck")]
    [SerializeField] private float thoiGianPhatHienKet   = 1.2f;
    [SerializeField] private float nguongDisplacementKet  = 0.3f;

    public static IReadOnlyList<TankPlayer> AllPlayers => _allPlayers;
    private static readonly List<TankPlayer> _allPlayers = new List<TankPlayer>();

    private BotContext          ctx;
    private BotSense            sense;
    private TankPlayer          tankPlayer;
    private Rigidbody2D         rb;
    private BotTurretController turretController;
    private BotShooter          botShooter;

    private IBotState stateTuanTra;
    private IBotState stateGiaoTranh;
    private IBotState stateNhatCoin;
    private IBotState stateRutLui;
    private IBotState currentState;

    private float      _timerDanhGia;
    private float      _deltaTichLuy;
    private BotCommand _currentCommand    = new BotCommand();
    private float      _steerNeTuongTruoc = 0f;

    private float   _stuckTimer      = 0f;
    private float   _checkStuckTimer = 0f;
    private Vector2 _viTriKiemTraCu  = Vector2.zero;
    private bool    _dangThoatKet    = false;
    private float   _thoatKetTimer   = 0f;
    private float   _steerThoatKet   = 1f;

    private const float STUCK_CHECK_INTERVAL = 0.5f;
    private const float THOI_GIAN_LUI_THOAT  = 0.6f;
    private const float THOI_GIAN_XOAY_THOAT = 0.4f;

    private void Awake()
    {
        tankPlayer       = GetComponent<TankPlayer>();
        sense            = GetComponent<BotSense>();
        rb               = GetComponent<Rigidbody2D>();
        turretController = GetComponent<BotTurretController>();
        botShooter       = GetComponent<BotShooter>();

        stateTuanTra   = new TrangThaiTuanTra();
        stateGiaoTranh = new TrangThaiGiaoTranh();
        stateNhatCoin  = new TrangThaiNhatCoin();
        stateRutLui    = new TrangThaiRutLui();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }

        TankPlayer.OnPlayerSpawned   += OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned += OnPlayerDespawned;

        if (!_allPlayers.Contains(tankPlayer))
            _allPlayers.Add(tankPlayer);

        ctx = new BotContext
        {
            Player          = tankPlayer,
            BodyTransform   = transform,
            TurretTransform = GetComponent<NguoiChoiNgamBan>()?.TurretTransform,
            Health          = tankPlayer.Health,
            Wallet          = tankPlayer.Wallet,
            LayerMaskTuong  = layerMaskTuong,
        };

        // Fallback: BotSpawner chưa gọi GanConfig → dùng configMacDinh (kéo Medium asset vào prefab)
        if (configMacDinh != null)
            GanConfig(configMacDinh);

        _timerDanhGia   = RandomChuKy();
        _viTriKiemTraCu = (Vector2)transform.position;
        ChuyenTrangThai(stateTuanTra);
    }

    public override void OnNetworkDespawn()
    {
        TankPlayer.OnPlayerSpawned   -= OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned -= OnPlayerDespawned;
        _allPlayers.Remove(tankPlayer);
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Gán config độ khó. BotSpawner gọi ngay sau Spawn():
    ///   botInstance.GetComponent&lt;BotBrain&gt;()?.GanConfig(pickedConfig);
    /// </summary>
    public void GanConfig(BotConfig config)
    {
        if (config == null) 
        {
            Debug.LogWarning("[BotBrain] GanConfig: config null, bỏ qua.");
            return;
        }

        // Nếu ctx chưa sẵn (gọi trước OnNetworkSpawn), chỉ lưu vào configMacDinh
        // để OnNetworkSpawn tự gọi lại sau.
        if (ctx == null)
        {
            configMacDinh = config;
            return;
        }

        ctx.Config = config;
        tankPlayer.Health?.DatMauToiDa(config.mauToiDa);
        GetComponent<BoPhongDan>()?.DatTanSuatBot(1f / config.thoiGianGiuaHaiVien);

        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} GanConfig → " +
                  $"mau={config.mauToiDa}  delay={config.thoiGianGiuaHaiVien}s  " +
                  $"saiSo=±{config.saiSoNgamDo}°  rutLui={config.nguongRutLui:P0}");
    }
    // ─────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsServer) { return; }
        if (ctx == null) { return; }

        float dt = Time.deltaTime;
        _timerDanhGia -= dt;
        _deltaTichLuy += dt;

        CapNhatAntiStuck(dt);

        if (_timerDanhGia <= 0f)
        {
            _timerDanhGia = RandomChuKy();
            ctx.DeltaTime = _deltaTichLuy;
            _deltaTichLuy = 0f;

            sense.DocMoiTruong(ctx);
            ChonTrangThai();

            _currentCommand = currentState.Update(ctx);

            // Áp dụng sai số ngắm từ config (1 lần / chu kỳ đánh giá)
            ApDungSaiSoNgam(_currentCommand);

            ctx.OutputHuongDiChuyen = _currentCommand.MoveInput;
            ctx.OutputDiemNgam      = _currentCommand.AimTarget ?? ctx.BotPosition;
            ctx.OutputCoBopCo       = _currentCommand.Fire;

            turretController?.DatContext(ctx);
            botShooter?.XuLyBan(_currentCommand.Fire);

            CapNhatLabelDebug();
        }

        ThucThiLenh(_currentCommand);
    }

    /// <summary>
    /// Lệch điểm ngắm một góc ngẫu nhiên ±saiSoNgamDo° quanh mục tiêu thực.
    /// Gọi 1 lần / chu kỳ đánh giá — không cộng dồn qua từng frame.
    /// </summary>
    private void ApDungSaiSoNgam(BotCommand cmd)
    {
        if (cmd.AimTarget == null) { return; }
        if (ctx.Config == null)    { return; }

        float saiSo = ctx.Config.saiSoNgamDo;
        if (saiSo <= 0f) { return; }

        Vector2 huong = cmd.AimTarget.Value - ctx.BotPosition;
        float   dist  = huong.magnitude;
        if (dist < 0.01f) { return; }

        float   rad  = Random.Range(-saiSo, saiSo) * Mathf.Deg2Rad;
        float   cos  = Mathf.Cos(rad);
        float   sin  = Mathf.Sin(rad);
        Vector2 moi  = new Vector2(
            huong.x * cos - huong.y * sin,
            huong.x * sin + huong.y * cos
        ).normalized;

        cmd.AimTarget = ctx.BotPosition + moi * dist;
    }

    private void ChonTrangThai()
    {
        // Đọc ngưỡng rút lui từ config; fallback 0.35f nếu chưa có config
        float nguongRutLui = ctx.Config != null ? ctx.Config.nguongRutLui : 0.35f;

        IBotState muon;

        if (ctx.HealthRatio < nguongRutLui)
            muon = stateRutLui;
        else if (ctx.NearestEnemy != null && ctx.DistanceToEnemy < banKinhGiaoTranh)
            muon = stateGiaoTranh;
        else if (!ctx.DuCoinDeBan(chiPhiBan) && ctx.NearestCoin != null)
            muon = stateNhatCoin;
        else
            muon = stateTuanTra;

        if (muon != currentState)
            ChuyenTrangThai(muon);
    }

    private void ChuyenTrangThai(IBotState tiepTheo)
    {
        currentState?.OnExit(ctx);
        currentState = tiepTheo;
        currentState.OnEnter(ctx);
        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} → {TenTrangThai(currentState)}");
    }

    private void CapNhatAntiStuck(float dt)
    {
        if (_dangThoatKet) { return; }

        _checkStuckTimer += dt;
        if (_checkStuckTimer < STUCK_CHECK_INTERVAL) { return; }

        _checkStuckTimer = 0f;

        Vector2 viTriHienTai = ctx != null ? ctx.BotPosition : (Vector2)transform.position;
        float   displacement = Vector2.Distance(viTriHienTai, _viTriKiemTraCu);
        _viTriKiemTraCu = viTriHienTai;

        bool dangDiChuyen = Mathf.Abs(_currentCommand.MoveInput.y) > 0.05f;
        if (!dangDiChuyen) { _stuckTimer = 0f; return; }

        if (displacement < nguongDisplacementKet)
            _stuckTimer += STUCK_CHECK_INTERVAL;
        else
            _stuckTimer = Mathf.Max(0f, _stuckTimer - STUCK_CHECK_INTERVAL * 0.5f);

        if (_stuckTimer >= thoiGianPhatHienKet)
            KichHoatThoatKet();
    }

    private void KichHoatThoatKet()
    {
        _stuckTimer    = 0f;
        _dangThoatKet  = true;
        _thoatKetTimer = THOI_GIAN_LUI_THOAT + THOI_GIAN_XOAY_THOAT;
        _steerThoatKet = Random.value > 0.5f ? 1f : -1f;
        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} → Kích hoạt thoát kẹt!");
    }

    private BotCommand LayLenhThoatKet(float dt)
    {
        if (!_dangThoatKet) { return null; }

        _thoatKetTimer -= dt;

        if (_thoatKetTimer <= 0f)
        {
            _dangThoatKet = false;
            _stuckTimer   = 0f;
            return null;
        }

        var cmd = new BotCommand();
        cmd.MoveInput = _thoatKetTimer > THOI_GIAN_XOAY_THOAT
            ? new Vector2(_steerThoatKet, -1f)
            : new Vector2(_steerThoatKet,  0f);

        return cmd;
    }

    private void ThucThiLenh(BotCommand cmd)
    {
        if (rb == null) { return; }

        float dt = Time.deltaTime;

        BotCommand thoatKetCmd = LayLenhThoatKet(dt);
        if (thoatKetCmd != null) { cmd = thoatKetCmd; }

        float steer    = cmd.MoveInput.x;
        float throttle = cmd.MoveInput.y;
        const float TOC_DO     = 5f;
        const float TOC_DO_XOY = 120f;

        float steerNe = TinhSteerNeTuong(throttle, out bool khanCap);
        _steerNeTuongTruoc = Mathf.Lerp(_steerNeTuongTruoc, steerNe, 8f * dt);

        float urgency    = khanCap ? 1f : Mathf.Abs(_steerNeTuongTruoc);
        float throttleNe = throttle * (1f - urgency * 0.75f);

        if (khanCap)
        {
            steer    = Mathf.Sign(steerNe + 0.001f);
            throttle = throttleNe;
        }
        else
        {
            steer    = Mathf.Lerp(steer, Mathf.Sign(_steerNeTuongTruoc + 0.001f), urgency);
            throttle = throttleNe;
        }

        ctx.BodyTransform.Rotate(0f, 0f, steer * -TOC_DO_XOY * dt);
        rb.velocity = (Vector2)ctx.BodyTransform.up * throttle * TOC_DO;
    }

    private float TinhSteerNeTuong(float throttle, out bool khanCap)
    {
        khanCap = false;
        if (Mathf.Abs(throttle) < 0.05f) { return 0f; }
        if (layerMaskTuong.value == 0)   { return 0f; }

        Vector2 viTri     = ctx.BotPosition;
        Vector2 huongTien = (Vector2)ctx.BodyTransform.up;
        if (throttle < 0f) { huongTien = -huongTien; }

        float[] gocKhanCap = { 0f, 30f, -30f };
        Vector2 lucKhanCap = Vector2.zero;
        foreach (float g in gocKhanCap)
        {
            Vector2      huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            RaycastHit2D hit   = Physics2D.Raycast(viTri, huong, khoangKhanCap, layerMaskTuong);
            if (hit.collider == null) { continue; }

            khanCap = true;
            float t = 1f - (hit.distance / khoangKhanCap);
            lucKhanCap += hit.normal * (t * t);
        }

        if (khanCap && lucKhanCap.sqrMagnitude > 0.0001f)
        {
            float gocKC = Vector2.SignedAngle(huongTien, lucKhanCap.normalized);
            return gocKC > 0f ? 1f : -1f;
        }

        float[] cacGoc     = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
        Vector2 lucDayTong = Vector2.zero;
        bool    coTuong    = false;

        foreach (float g in cacGoc)
        {
            Vector2      huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            RaycastHit2D hit   = Physics2D.Raycast(viTri, huong, khoangNeTuong, layerMaskTuong);
            if (hit.collider == null) { continue; }

            coTuong = true;
            float t    = 1f - (hit.distance / khoangNeTuong);
            float manh = t * t;
            lucDayTong += hit.normal * manh;
        }

        if (!coTuong || lucDayTong.sqrMagnitude < 0.0001f) { return 0f; }

        float goc     = Vector2.SignedAngle(huongTien, lucDayTong.normalized);
        float huongNe = goc > 0f ? 1f : -1f;
        float cuongDo = Mathf.Clamp01(lucDayTong.magnitude);
        return huongNe * cuongDo;
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

    public void SetLayerMaskTuong(LayerMask mask)
    {
        layerMaskTuong = mask;
        if (ctx != null) { ctx.LayerMaskTuong = mask; }
    }

    private float RandomChuKy() => Random.Range(chuKyDanhGiaMin, chuKyDanhGiaMax);

    private static void OnPlayerSpawned(TankPlayer p)
    {
        if (!_allPlayers.Contains(p)) { _allPlayers.Add(p); }
    }

    private static void OnPlayerDespawned(TankPlayer p)
    {
        _allPlayers.Remove(p);
    }
}
