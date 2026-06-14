using System.Collections.Generic;
using UnityEngine;

public class BotContext
{
    public TankPlayer Player          { get; set; }
    public Transform  BodyTransform   { get; set; }
    public Transform  TurretTransform { get; set; }
    public Mau        Health          { get; set; }
    public CoinWallet Wallet          { get; set; }

    /// <summary>Config độ khó gán qua BotBrain.GanConfig(). Null = chưa gán.</summary>
    public BotConfig Config { get; set; }

    public TankPlayer       NearestEnemy    { get; set; }
    public Vector2          EnemyPosition   { get; set; }
    public float            DistanceToEnemy { get; set; } = float.MaxValue;
    public List<TankPlayer> DanhSachDichGan { get; }      = new List<TankPlayer>();

    public Coin        NearestCoin     { get; set; }
    public Vector2     CoinPosition    { get; set; }
    public float       DistanceToCoin  { get; set; } = float.MaxValue;
    public List<Coin>  DanhSachCoinGan { get; }      = new List<Coin>();
    public int         SoCoinHienTai   { get; set; }

    public HealingZone NearestHealingZone    { get; set; }
    public Vector2     HealingZonePosition   { get; set; }
    public float       DistanceToHealingZone { get; set; } = float.MaxValue;

    public Vector2 BotPosition => BodyTransform != null ? (Vector2)BodyTransform.position : Vector2.zero;

    public int   CurrentHealth => Health != null ? Health.MauHienTai.Value : 0;
    public int   MaxHealth     => Health != null ? Health.MauToiDa         : 1;
    public float HealthRatio   => (float)CurrentHealth / Mathf.Max(1, MaxHealth);

    public bool DuCoinDeBan(int chiPhiBan) => SoCoinHienTai >= chiPhiBan;

    public Vector2  OutputHuongDiChuyen { get; set; }
    public Vector2  OutputDiemNgam      { get; set; }
    public bool     OutputCoBopCo       { get; set; }
    public float    DeltaTime           { get; set; }
    public LayerMask LayerMaskTuong     { get; set; }
}
