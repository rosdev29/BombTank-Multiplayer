using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealingZone : NetworkBehaviour
{
    private static readonly List<HealingZone> zones = new List<HealingZone>();

    public static IReadOnlyList<HealingZone> AllZones => zones;
    public Vector2 Position => transform.position;
    public bool CoTheHoiMau => HealPower.Value > 0;

    [Header("References")]
    [SerializeField] private Image healPowerBar;

    [Header("Settings")]
    [SerializeField] private int maxHealPower = 30;
    [SerializeField] private float healCooldown = 60f;
    [SerializeField] private float healTickRate = 1f;
    [SerializeField] private int coinsPerTick = 10;
    [SerializeField] private int healthPerTick = 10;

    private float remainingCooldown;
    private float tickTimer;

    private NetworkVariable<int> HealPower = new NetworkVariable<int>();

    private void OnEnable()
    {
        if (!zones.Contains(this))
            zones.Add(this);
    }

    private void OnDisable()
    {
        zones.Remove(this);
    }

    private void Awake()
    {
        if (healPowerBar != null && maxHealPower > 0)
            healPowerBar.fillAmount = 1f;
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            HealPower.OnValueChanged += HandleHealPowerChanged;
            HandleHealPowerChanged(0, HealPower.Value);
        }

        if (IsServer)
            HealPower.Value = maxHealPower;
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
            HealPower.OnValueChanged -= HandleHealPowerChanged;
    }

    private bool IsPlayerInsideZone(TankPlayer player, Collider2D zoneCollider)
    {
        if (player == null || zoneCollider == null || !player.IsSpawned)
            return false;

        // Dùng vị trí thân tank — không phụ thuộc collider con (tháp pháo, đạn) thoát trigger khi bắn.
        return zoneCollider.OverlapPoint(player.transform.position);
    }

    private void CollectPlayersInZone(List<TankPlayer> buffer)
    {
        buffer.Clear();

        Collider2D zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider == null)
            return;

        TankPlayer[] allPlayers = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in allPlayers)
        {
            if (IsPlayerInsideZone(player, zoneCollider))
                buffer.Add(player);
        }
    }

    private readonly List<TankPlayer> playersInZone = new List<TankPlayer>();

    private void Update()
    {
        if (!IsServer || !IsSpawned) { return; }
        if (MatchEndBridge.IsMatchEnded) { return; }
        if (healTickRate <= 0f) { return; }

        if (remainingCooldown > 0f)
        {
            remainingCooldown -= Time.deltaTime;
            if (remainingCooldown <= 0f)
                HealPower.Value = maxHealPower;
            else
                return;
        }

        float tickInterval = 1f / healTickRate;
        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) { return; }

        CollectPlayersInZone(playersInZone);

        foreach (TankPlayer player in playersInZone)
        {
            if (player == null || player.Health == null || player.Wallet == null) { continue; }
            if (HealPower.Value == 0) { break; }

            int maxHealth = player.Health.MauToiDaNet.Value;
            if (player.Health.MauHienTai.Value >= maxHealth) { continue; }

            int availableCoins = player.Wallet.TotalCoins.Value;
            if (availableCoins <= 0) { continue; }

            int coinsSpent = Mathf.Min(coinsPerTick, availableCoins);
            int healthGain = Mathf.Max(1, Mathf.RoundToInt(healthPerTick * (coinsSpent / (float)coinsPerTick)));

            player.Wallet.SpendCoins(coinsSpent);
            player.Health.HoiMau(healthGain);

            HealPower.Value -= 1;

            if (HealPower.Value == 0)
                remainingCooldown = healCooldown;
        }

        tickTimer = tickTimer % tickInterval;
    }

    private void HandleHealPowerChanged(int oldHealPower, int newHealPower)
    {
        if (healPowerBar == null || maxHealPower <= 0) { return; }
        healPowerBar.fillAmount = (float)newHealPower / maxHealPower;
    }
}
