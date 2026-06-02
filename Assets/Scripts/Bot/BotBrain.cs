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
    [SerializeField] private float banKinhGiaoTranh = 10f;

    [Header("Debug")]
    [SerializeField] private TextMeshPro labelTrangThai;

    public static IReadOnlyList<TankPlayer> AllPlayers => _allPlayers;
    private static readonly List<TankPlayer> _allPlayers = new List<TankPlayer>();

    private BotContext ctx;
    private BotSense sense;
    private TankPlayer tankPlayer;
    private Rigidbody2D rb;
    private BoPhongDan boPhongDan;
    private Transform turretTransform;

    private IBotState stateTuanTra;
    private IBotState stateGiaoTranh;
    private IBotState stateNhatCoin;
    private IBotState stateRutLui;
    private IBotState currentState;

    private float _timerDanhGia;

    private void Awake()
    {
        tankPlayer = GetComponent<TankPlayer>();
        sense = GetComponent<BotSense>();
        rb = GetComponent<Rigidbody2D>();
        boPhongDan = GetComponent<BoPhongDan>();

        Transform turretPivot = transform.Find("TurretPivot");
        if (turretPivot != null)
        {
            turretTransform = turretPivot;
        }

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

        TankPlayer.OnPlayerSpawned += OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned += OnPlayerDespawned;

        if (!_allPlayers.Contains(tankPlayer))
        {
            _allPlayers.Add(tankPlayer);
        }

        ctx = new BotContext
        {
            Player = tankPlayer,
            BodyTransform = transform,
            Health = tankPlayer.Health,
            Wallet = tankPlayer.Wallet,
        };

        _timerDanhGia = RandomChuKy();
        ChuyenTrangThai(stateTuanTra);
    }

    public override void OnNetworkDespawn()
    {
        TankPlayer.OnPlayerSpawned -= OnPlayerSpawned;
        TankPlayer.OnPlayerDespawned -= OnPlayerDespawned;

        _allPlayers.Remove(tankPlayer);
    }

    private void Update()
    {
        if (!IsServer) { return; }
        if (ctx == null) { return; }

        _timerDanhGia -= Time.deltaTime;
        if (_timerDanhGia > 0f) { return; }

        _timerDanhGia = RandomChuKy();
        ctx.DeltaTime = Time.deltaTime;

        sense.DocMoiTruong(ctx);
        ChonTrangThai();

        BotCommand cmd = currentState.Update(ctx);

        ctx.OutputHuongDiChuyen = cmd.MoveInput;
        ctx.OutputDiemNgam = cmd.AimTarget ?? ctx.BotPosition;
        ctx.OutputCoBopCo = cmd.Fire;

        ThucThiLenh(cmd);
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

        float steer = cmd.MoveInput.x;
        float throttle = cmd.MoveInput.y;
        float tocDo = 5f;
        float tocDoXoay = 120f;

        ctx.BodyTransform.Rotate(0f, 0f, steer * -tocDoXoay * Time.deltaTime);
        rb.velocity = (Vector2)ctx.BodyTransform.up * throttle * tocDo;

        if (turretTransform != null)
        {
            Vector2 aimDirection = ctx.OutputDiemNgam - (Vector2)turretTransform.position;

            if (aimDirection.sqrMagnitude > 0.01f)
            {
                turretTransform.up = aimDirection;
            }
        }

        if (cmd.Fire)
        {
            Debug.Log("BOT FIRE COMMAND: " + tankPlayer.PlayerName.Value);
        }

        if (cmd.Fire && boPhongDan != null)
        {
            boPhongDan.BotFire();
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
        TrangThaiNhatCoin => "💰 Nhặt coin",
        TrangThaiRutLui => "🏃 Rút lui",
        TrangThaiTuanTra => "🔍 Tuần tra",
        _ => s?.GetType().Name ?? "null"
    };

    private float RandomChuKy() => Random.Range(chuKyDanhGiaMin, chuKyDanhGiaMax);

    private static void OnPlayerSpawned(TankPlayer p)
    {
        if (!_allPlayers.Contains(p))
        {
            _allPlayers.Add(p);
        }
    }

    private static void OnPlayerDespawned(TankPlayer p)
    {
        _allPlayers.Remove(p);

    }


}