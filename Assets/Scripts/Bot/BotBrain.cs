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
public class BotBrain : NetworkBehaviour
{
    [Header("Chu kỳ đánh giá")]
    [SerializeField] private float chuKyDanhGiaMin = 0.2f;
    [SerializeField] private float chuKyDanhGiaMax = 0.5f;

    [Header("Ngưỡng chuyển trạng thái")]
    [SerializeField] private float nguongMauThapDeRutLui = 0.35f;
    [SerializeField] private float banKinhGiaoTranh = 20f;
    [SerializeField] private int chiPhiBan = 1;

    [Header("Cấu hình Perception")]
    [SerializeField] private float banKinhPhatHienDich = 20f;
    [SerializeField] private float banKinhPhatHienCoin = 20f;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    // BotBrain không tự quản lý player list — dùng TankPlayer.AllTankPlayers

    // ── Component references ────────────────────────────────────────────────
    private BotContext ctx;
    private BotSense sense;
    private BotMover botMover;
    private TankPlayer tankPlayer;
    private BotTurretController turretController;
    private BotShooter botShooter;

    // ── FSM states ──────────────────────────────────────────────────────────
    private IBotState stateTuanTra;
    private IBotState stateGiaoTranh;
    private IBotState stateNhatCoin;
    private IBotState stateRutLui;
    private IBotState currentState;

    // ── Priority transitions (duyệt theo Priority giảm dần) ─────────────────
    private List<IBotStateTransition> _transitions;

    // ── Timing ──────────────────────────────────────────────────────────────
    private float _timerDanhGia;
    private float _deltaTichLuy;
    private BotCommand _currentCommand = new BotCommand();

    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        tankPlayer = GetComponent<TankPlayer>();
        sense = GetComponent<BotSense>();
        botMover = GetComponent<BotMover>();
        turretController = GetComponent<BotTurretController>();
        botShooter = GetComponent<BotShooter>();

        stateTuanTra = new TrangThaiTuanTra();
        stateGiaoTranh = new TrangThaiGiaoTranh();
        stateNhatCoin = new TrangThaiNhatCoin();
        stateRutLui = new TrangThaiRutLui();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }


        // Khởi tạo Blackboard
        ctx = new BotContext
        {
            Player = tankPlayer,
            BodyTransform = transform,
            TurretTransform = GetComponent<NguoiChoiNgamBan>()?.TurretTransform,
            Health = tankPlayer.Health,
            Wallet = tankPlayer.Wallet,
            LayerMaskTuong = LayerMask.GetMask("Terrain"),
            BanKinhPhatHienDich = banKinhPhatHienDich,
            BanKinhPhatHienCoin = banKinhPhatHienCoin,
            Pathfinder = GetComponent<BotPathfinder>()
        };

        ctx.Pathfinder.Init(ctx);

        // Truyền context + layermask sang BotMover
        botMover.KhoiTao(ctx, ctx.LayerMaskTuong);

        // Khởi tạo Priority transitions (Priority cao → kiểm tra trước)
        _transitions = new List<IBotStateTransition>
        {
            new ChuyenRutLui   (stateRutLui,    priority: 40, nguongMauThap: nguongMauThapDeRutLui),
            new ChuyenGiaoTranh(stateGiaoTranh, priority: 30, banKinh: banKinhGiaoTranh, chiPhi: chiPhiBan),
            new ChuyenNhatCoin (stateNhatCoin,  priority: 20, chiPhi: chiPhiBan),
            new ChuyenTuanTra  (stateTuanTra,   priority: 10),   // fallback luôn true
        };
        _transitions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        _timerDanhGia = RandomChuKy();
        ChuyenTrangThai(stateTuanTra);
    }

    public override void OnNetworkDespawn() { }

    private void Update()
    {
        if (!IsServer || ctx == null) { return; }

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

            ctx.OutputHuongDiChuyen = _currentCommand.MoveInput;
            ctx.OutputDiemNgam = _currentCommand.AimTarget ?? ctx.BotPosition;
            ctx.OutputCoBopCo = _currentCommand.Fire;

            turretController?.DatContext(ctx);
            botShooter?.XuLyBan(_currentCommand.Fire);

            CapNhatLabelDebug();
        }

        // Act: BotMover thực thi lệnh mỗi frame (để chuyển động mượt)
        botMover.ThucThiLenh(_currentCommand);
    }

    // ── Priority FSM ─────────────────────────────────────────────────────────

    private void ChonTrangThaiTheoPriority()
    {
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
        TrangThaiNhatCoin => "💰 Nhặt coin",
        TrangThaiRutLui => "🏃 Rút lui",
        TrangThaiTuanTra => "🔍 Tuần tra",
        _ => s?.GetType().Name ?? "null"
    };

    private float RandomChuKy() => Random.Range(chuKyDanhGiaMin, chuKyDanhGiaMax);


    // ── Transition implementations (nested classes) ───────────────────────────

    private sealed class ChuyenRutLui : IBotStateTransition
    {
        public int Priority { get; }
        public IBotState State { get; }
        private readonly float _nguongMauThap;

        public ChuyenRutLui(IBotState state, int priority, float nguongMauThap)
        {
            State = state;
            Priority = priority;
            _nguongMauThap = nguongMauThap;
        }

        public bool CanEnter(BotContext ctx) =>
            ctx.HealthRatio < _nguongMauThap;
    }

    private sealed class ChuyenGiaoTranh : IBotStateTransition
    {
        public int Priority { get; }
        public IBotState State { get; }
        private readonly float _banKinh;
        private readonly int _chiPhi;

        public ChuyenGiaoTranh(IBotState state, int priority, float banKinh, int chiPhi)
        {
            State = state;
            Priority = priority;
            _banKinh = banKinh;
            _chiPhi = chiPhi;
        }

        public bool CanEnter(BotContext ctx) =>
            ctx.NearestEnemy != null
            && ctx.DistanceToEnemy < _banKinh
            && ctx.DuCoinDeBan(_chiPhi);
    }

    private sealed class ChuyenNhatCoin : IBotStateTransition
    {
        public int Priority { get; }
        public IBotState State { get; }
        private readonly int _chiPhi;

        public ChuyenNhatCoin(IBotState state, int priority, int chiPhi)
        {
            State = state;
            Priority = priority;
            _chiPhi = chiPhi;
        }

        public bool CanEnter(BotContext ctx) =>
            !ctx.DuCoinDeBan(_chiPhi) && ctx.NearestCoin != null;
    }

    private sealed class ChuyenTuanTra : IBotStateTransition
    {
        public int Priority { get; }
        public IBotState State { get; }

        public ChuyenTuanTra(IBotState state, int priority)
        {
            State = state;
            Priority = priority;
        }

        // Fallback: luôn true — tuần tra khi không có điều kiện nào khác thoả
        public bool CanEnter(BotContext ctx) => true;
    }
}