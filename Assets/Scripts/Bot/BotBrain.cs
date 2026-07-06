using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// Não bot — chỉ chịu trách nhiệm Think:
///   Sense  → BotSense.DocMoiTruong() cập nhật BotContext
///   Think  → Priority FSM chọn state → state.Update() trả về BotCommand
///   Act    → BotMover.ThucThiLenh() + BotMover.CapNhatAntiStuck() thực thi lệnh
/// </summary>
[RequireComponent(typeof(TankPlayer))]
[RequireComponent(typeof(BotSense))]
[RequireComponent(typeof(BotMover))]
[RequireComponent(typeof(BotPathfinder))]
public class BotBrain : MonoBehaviour
{
    [Header("Chu kỳ đánh giá")]
    [SerializeField] private float chuKyDanhGiaMin = 0.2f;
    [SerializeField] private float chuKyDanhGiaMax = 0.5f;

    [Header("Ngưỡng chuyển trạng thái")]
    // nguongMauThapDeRutLui đọc từ ctx.Config.nguongRutLui (fallback 0.35f)
    [SerializeField] private float banKinhGiaoTranh = 20f;
    [SerializeField] private int   chiPhiBan        = 5;

    [Header("Config độ khó (fallback dev)")]
    [Tooltip("Kéo BotConfig_Medium vào đây. GanConfig() tự gọi trong OnNetworkSpawn nếu BotSpawner chưa wire.")]
    [SerializeField] private BotConfig configMacDinh;

    [Header("Cấu hình Perception")]
    [SerializeField] private float banKinhPhatHienDich = 20f;
    [SerializeField] private float banKinhPhatHienCoin = 20f;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    // BotBrain không tự quản lý player list — dùng TankPlayer.AllTankPlayers

    // ── Component references ────────────────────────────────────────────────
    private BotContext          ctx;
    private BotSense            sense;
    private BotMover            botMover;
    private TankPlayer          tankPlayer;
    private BotTurretController turretController;
    private BotShooter          botShooter;

    // ── FSM states ──────────────────────────────────────────────────────────
    private IBotState stateGiaoTranh;
    private IBotState stateNhatCoin;
    private IBotState stateRutLui;
    private IBotState currentState;

    // ── Priority transitions (duyệt theo Priority giảm dần) ─────────────────
    private List<IBotStateTransition> _transitions;

    // ── Timing ──────────────────────────────────────────────────────────────
    private float      _timerDanhGia;
    private float      _deltaTichLuy;
    private BotCommand _currentCommand = new BotCommand();

    private NetworkObject _networkObject;
    private bool _initialized;

    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        tankPlayer       = GetComponent<TankPlayer>();
        sense            = GetComponent<BotSense>();
        botMover         = GetComponent<BotMover>();
        turretController = GetComponent<BotTurretController>();
        botShooter       = GetComponent<BotShooter>();

        stateGiaoTranh = new TrangThaiGiaoTranh();
        stateNhatCoin  = new TrangThaiNhatCoin();
        stateRutLui    = new TrangThaiRutLui();
    }

    private void EnsureInitialized()
    {
        if (_initialized) { return; }

        _networkObject = GetComponent<NetworkObject>();
        if (_networkObject == null || !_networkObject.IsSpawned) { return; }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            _initialized = true;
            return;
        }

        // Khởi tạo Blackboard
        ctx = new BotContext
        {
            Player               = tankPlayer,
            BodyTransform        = transform,
            TurretTransform      = GetComponent<NguoiChoiNgamBan>()?.TurretTransform,
            Health               = tankPlayer.Health,
            Wallet               = tankPlayer.Wallet,
            LayerMaskTuong       = LayerMask.GetMask("Terrain"),
            BanKinhPhatHienDich  = banKinhPhatHienDich,
            BanKinhPhatHienCoin  = banKinhPhatHienCoin,
            Pathfinder           = GetComponent<BotPathfinder>(),
        };

        ctx.Pathfinder.Init(ctx);

        BoPhongDan combat = GetComponent<BoPhongDan>();
        if (combat != null)
        {
            chiPhiBan = combat.GetShootingCost();
        }

        ctx.ChiPhiBan = chiPhiBan;

        // Truyền context + layermask sang BotMover
        botMover.KhoiTao(ctx, ctx.LayerMaskTuong);

        // Fallback: BotSpawner chưa gọi GanConfig → dùng configMacDinh
        if (configMacDinh != null)
            GanConfig(configMacDinh);

        // Khởi tạo Priority transitions (Priority cao → kiểm tra trước)
        _transitions = new List<IBotStateTransition>
        {
            new ChuyenRutLui            (stateRutLui,    priority: 40),
            new ChuyenRutLuiHetCoin     (stateRutLui,    priority: 35, banKinh: banKinhGiaoTranh, chiPhi: chiPhiBan),
            new ChuyenGiaoTranh         (stateGiaoTranh, priority: 30, banKinh: banKinhGiaoTranh, chiPhi: chiPhiBan),
            new ChuyenNhatCoin          (stateNhatCoin,  priority: 10),
        };
        _transitions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        _timerDanhGia = RandomChuKy();
        ChuyenTrangThai(stateNhatCoin);
        _initialized = true;
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
        EnsureInitialized();
        if (!_initialized || ctx == null) { return; }
        if (_networkObject == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }

        if (MatchEndBridge.IsMatchEnded)
        {
            _currentCommand = new BotCommand();
            botMover.ThucThiLenh(_currentCommand);
            return;
        }

        float dt = Time.deltaTime;
        _timerDanhGia -= dt;
        _deltaTichLuy += dt;

        // Anti-stuck cập nhật mỗi frame (trước ThucThiLenh)
        botMover.CapNhatAntiStuck(dt, _currentCommand);

        if (_timerDanhGia <= 0f)
        {
            _timerDanhGia = RandomChuKy();
            ctx.DeltaTime = _deltaTichLuy;
            _deltaTichLuy = 0f;

            // Sense
            sense.DocMoiTruong(ctx);

            // Think: chọn state theo priority
            ChonTrangThaiTheoPriority();

            // Think: state hiện tại ra lệnh
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

        if (currentState == stateGiaoTranh || currentState == stateRutLui)
        {
            ThuFireKhiNgam();
        }

        // Act: BotMover thực thi lệnh mỗi frame (để chuyển động mượt)
        botMover.ThucThiLenh(_currentCommand);
    }

    private void ThuFireKhiNgam()
    {
        if (ctx == null || ctx.NearestEnemy == null) { return; }
        if (ctx.Wallet == null || ctx.Wallet.TotalCoins.Value < chiPhiBan) { return; }
        if (_currentCommand.AimTarget == null) { return; }

        if (!BotSteering.CoDuongThong(ctx.BotPosition, ctx.EnemyPosition)) { return; }

        Transform nongNgam = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
        Vector2 huongNgam = (_currentCommand.AimTarget.Value - ctx.BotPosition).normalized;
        float gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);
        float nguongGoc = ctx.DistanceToEnemy < 10f ? 28f : 12f;

        if (gocLechNgam < nguongGoc)
        {
            botShooter?.XuLyBan(true);
        }
    }

    // ── Priority FSM ─────────────────────────────────────────────────────────

    private void ChonTrangThaiTheoPriority()
    {
        ctx.IsRetreating = currentState == stateRutLui;

        foreach (IBotStateTransition t in _transitions)
        {
            if (t.CanEnter(ctx))
            {
                if (t.State != currentState)
                    ChuyenTrangThai(t.State);
                return;
            }
        }
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

    private void ChuyenTrangThai(IBotState tiepTheo)
    {
        currentState?.OnExit(ctx);
        currentState = tiepTheo;
        currentState.OnEnter(ctx);
        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} → {TenTrangThai(currentState)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
        _                  => s?.GetType().Name ?? "null"
    };

    private float RandomChuKy() => Random.Range(chuKyDanhGiaMin, chuKyDanhGiaMax);

    private static float LayNguongRutLui(BotContext botCtx) =>
        botCtx.Config != null ? botCtx.Config.nguongRutLui : 0.35f;

    private static float LayNguongThoatRutLui(BotContext botCtx) =>
        botCtx.Config != null ? botCtx.Config.nguongThoatRutLui : 0.45f;


    // ── Transition implementations (nested classes) ───────────────────────────

    private sealed class ChuyenRutLui : IBotStateTransition
    {
        public int       Priority { get; }
        public IBotState State    { get; }

        public ChuyenRutLui(IBotState state, int priority)
        {
            State    = state;
            Priority = priority;
        }

        public bool CanEnter(BotContext botCtx) =>
            botCtx.IsRetreating
                ? botCtx.HealthRatio < LayNguongThoatRutLui(botCtx)
                : botCtx.HealthRatio < LayNguongRutLui(botCtx);
    }

    /// <summary>
    /// Khi có địch gần mà HẾT coin để bắn → rút lui thay vì đứng nhặt coin.
    /// (Đổi từ ChuyenTronViHetCoin sang stateRutLui)
    /// </summary>
    private sealed class ChuyenRutLuiHetCoin : IBotStateTransition
    {
        public int       Priority { get; }
        public IBotState State    { get; }
        private readonly float _banKinh;
        private readonly int   _chiPhi;

        public ChuyenRutLuiHetCoin(IBotState state, int priority, float banKinh, int chiPhi)
        {
            State    = state;
            Priority = priority;
            _banKinh = banKinh;
            _chiPhi  = chiPhi;
        }

        public bool CanEnter(BotContext botCtx) =>
            botCtx.NearestEnemy != null
            && botCtx.DistanceToEnemy < _banKinh
            && !botCtx.DuCoinDeBan(_chiPhi);
    }

    private sealed class ChuyenGiaoTranh : IBotStateTransition
    {
        public int       Priority { get; }
        public IBotState State    { get; }
        private readonly float _banKinh;
        private readonly int   _chiPhi;

        public ChuyenGiaoTranh(IBotState state, int priority, float banKinh, int chiPhi)
        {
            State    = state;
            Priority = priority;
            _banKinh = banKinh;
            _chiPhi  = chiPhi;
        }

        public bool CanEnter(BotContext botCtx) =>
            botCtx.NearestEnemy != null
            && botCtx.DistanceToEnemy < _banKinh
            && botCtx.DuCoinDeBan(_chiPhi);
    }

    private sealed class ChuyenNhatCoin : IBotStateTransition
    {
        public int       Priority { get; }
        public IBotState State    { get; }

        public ChuyenNhatCoin(IBotState state, int priority)
        {
            State    = state;
            Priority = priority;
        }

        public bool CanEnter(BotContext botCtx) => true;
    }
}
