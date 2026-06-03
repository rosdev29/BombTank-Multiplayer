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
    [SerializeField] private float nguongMauThapDeRutLui = 0.35f;
    [SerializeField] private float banKinhGiaoTranh      = 10f;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    [Header("Né tường")]
    [Tooltip("Layer của tường/địa hình (Terrain). Bot sẽ raycast để tránh.")]
    [SerializeField] private LayerMask layerMaskTuong;
    [Tooltip("Khoảng cách bắt đầu bắt đầu tránh tường (met).")]
    [SerializeField] private float khoangNeTuong = 2.5f;

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

    private float _timerDanhGia;
    private float _deltaTichLuy;
    private BotCommand _currentCommand = new BotCommand();
    private float _steerNeTuongTruoc   = 0f; // smoothing né tường

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

        ctx = new BotContext
        {
            Player          = tankPlayer,
            BodyTransform   = transform,
            TurretTransform = GetComponent<NguoiChoiNgamBan>()?.TurretTransform,
            Health          = tankPlayer.Health,
            Wallet          = tankPlayer.Wallet,
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
        if (!IsServer) { return; }

        float dt = Time.deltaTime;
        _timerDanhGia -= dt;
        _deltaTichLuy += dt;

        // --- CHU KỲ ĐÁNH GIÁ AI (0.2s - 0.5s một lần) ---
        if (_timerDanhGia <= 0f)
        {
            _timerDanhGia    = RandomChuKy();
            ctx.DeltaTime    = _deltaTichLuy;  
            _deltaTichLuy    = 0f;             

            sense.DocMoiTruong(ctx);
            ChonTrangThai();

            _currentCommand = currentState.Update(ctx);

            ctx.OutputHuongDiChuyen = _currentCommand.MoveInput;
            ctx.OutputDiemNgam      = _currentCommand.AimTarget ?? ctx.BotPosition;
            ctx.OutputCoBopCo       = _currentCommand.Fire;

            // Xoay nòng súng về điểm ngắm
            turretController?.DatContext(ctx);

            // Bắn (chỉ check 1 lần mỗi chu kỳ đánh giá)
            botShooter?.XuLyBan(_currentCommand.Fire);

            CapNhatLabelDebug();
        }

        // --- THỰC THI LỆNH MỖI FRAME (Smooth Movement) ---
        // Phải chạy mỗi frame thì xe mới xoay mượt mà (Time.deltaTime)
        ThucThiLenh(_currentCommand);
    }

    private void ChonTrangThai()
    {
        IBotState muon;

        if (ctx.HealthRatio < nguongMauThapDeRutLui)
        {
            muon = stateRutLui;
        }
        else if (ctx.NearestEnemy != null && ctx.DistanceToEnemy < banKinhGiaoTranh)
        {
            muon = stateGiaoTranh;
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
    }

    private void ChuyenTrangThai(IBotState tiepTheo)
    {
        currentState?.OnExit(ctx);
        currentState = tiepTheo;
        currentState.OnEnter(ctx);

        Debug.Log($"[BotBrain] {tankPlayer.PlayerName.Value} → {TenTrangThai(currentState)}");
    }

    private void ThucThiLenh(BotCommand cmd)
    {
        if (rb == null) { return; }

        float steer    = cmd.MoveInput.x;
        float throttle = cmd.MoveInput.y;
        const float TOC_DO     = 5f;
        const float TOC_DO_XOY = 120f;

        // Né tường: tính lực đẩy tổng hợp rồi blend với lệnh AI
        float steerNe = TinhSteerNeTuong(throttle);
        // Smooth để tránh dao động (giật trái/phải liên tục)
        _steerNeTuongTruoc = Mathf.Lerp(_steerNeTuongTruoc, steerNe, 8f * Time.deltaTime);
        // Blend: steerNe mạnh thì né tường, yếu thì AI tự điều khiển
        float urgency = Mathf.Abs(_steerNeTuongTruoc);
        steer = Mathf.Lerp(steer, Mathf.Sign(_steerNeTuongTruoc + 0.001f), urgency);

        ctx.BodyTransform.Rotate(0f, 0f, steer * -TOC_DO_XOY * Time.deltaTime);
        rb.velocity = (Vector2)ctx.BodyTransform.up * throttle * TOC_DO;
    }

    private float TinhSteerNeTuong(float throttle)
    {
        if (Mathf.Abs(throttle) < 0.05f) { return 0f; }
        if (layerMaskTuong.value == 0)   { return 0f; }

        Vector2 viTri     = ctx.BotPosition;
        Vector2 huongTien = (Vector2)ctx.BodyTransform.up;
        if (throttle < 0f) { huongTien = -huongTien; }

        // 9 tia: thẳng, ±20°, ±40°, ±60°, ±80°
        float[] cacGoc  = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
        Vector2 lucDayTong = Vector2.zero;
        bool    coTuong    = false;

        foreach (float g in cacGoc)
        {
            Vector2      huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            RaycastHit2D hit   = Physics2D.Raycast(viTri, huong, khoangNeTuong, layerMaskTuong);
            if (hit.collider == null) { continue; }

            coTuong = true;
            // Trọng số bình phương: càng gần tường, lực đẩy càng lớn
            float t    = 1f - (hit.distance / khoangNeTuong);
            float manh = t * t;
            lucDayTong += hit.normal * manh;
        }

        if (!coTuong || lucDayTong.sqrMagnitude < 0.0001f) { return 0f; }

        // Chuyển vector lực đẩy thành góc lái (steer)
        float goc = Vector2.SignedAngle(huongTien, lucDayTong.normalized);
        // goc > 0 → tường đẩy sang trái → xe cần rẽ phải (steer = +1)
        // goc < 0 → tường đẩy sang phải → xe cần rẽ trái (steer = -1)
        float huongNe = goc > 0f ? 1f : -1f;

        // Cường độ phản ứng: tỉ lệ với tổng lực đẩy, clamp tối đa 1
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

    public void SetLayerMaskTuong(LayerMask mask) => layerMaskTuong = mask;

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
