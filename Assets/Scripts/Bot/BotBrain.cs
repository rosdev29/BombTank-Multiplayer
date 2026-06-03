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
    private float _deltaTichLuy;   // thời gian thực trôi qua giữa 2 chu kỳ bot

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
            Player        = tankPlayer,
            BodyTransform = transform,
            Health        = tankPlayer.Health,
            Wallet        = tankPlayer.Wallet,
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
        _deltaTichLuy += dt;   // cộng dồn mỗi frame

        if (_timerDanhGia > 0f) { return; }

        _timerDanhGia    = RandomChuKy();
        ctx.DeltaTime    = _deltaTichLuy;  // đúng: thời gian thực giữa 2 tick
        _deltaTichLuy    = 0f;             // reset sau khi dùng

        sense.DocMoiTruong(ctx);
        ChonTrangThai();

        BotCommand cmd = currentState.Update(ctx);

        ctx.OutputHuongDiChuyen = cmd.MoveInput;
        ctx.OutputDiemNgam      = cmd.AimTarget ?? ctx.BotPosition;
        ctx.OutputCoBopCo       = cmd.Fire;

        ThucThiLenh(cmd);

        // Xoay nòng súng về điểm ngắm
        turretController?.DatContext(ctx);

        // Bắn (chỉ 1 lần mỗi chu kỳ đánh giá, không phải mỗi frame)
        botShooter?.XuLyBan(cmd.Fire);

        CapNhatLabelDebug();
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

        float steer     = cmd.MoveInput.x;
        float throttle  = cmd.MoveInput.y;
        float tocDo     = 5f;
        float tocDoXoay = 120f;

        ctx.BodyTransform.Rotate(0f, 0f, steer * -tocDoXoay * Time.deltaTime);
        rb.velocity = (Vector2)ctx.BodyTransform.up * throttle * tocDo;
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
}
