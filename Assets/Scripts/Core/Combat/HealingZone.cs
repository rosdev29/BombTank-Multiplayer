using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealingZone : NetworkBehaviour
{
    // Danh sach tat ca vung hoi mau tren map (bot dung de tim vi tri gan nhat)
    private static readonly List<HealingZone> zones = new List<HealingZone>();

    public static IReadOnlyList<HealingZone> AllZones => zones;
    public Vector2 Position => transform.position;
    public bool CoTheHoiMau => HealPower.Value > 0; // false khi het nang luong hoac dang cooldown

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
    private HashSet<TankPlayer> playersInZone = new HashSet<TankPlayer>();

    private NetworkVariable<int> HealPower = new NetworkVariable<int>();

    // Dang ky / huy dang ky khi vung hoi mau bat/tat tren scene
    private void OnEnable()
    {
        if (!zones.Contains(this))
        {
            zones.Add(this);
        }
    }

    private void OnDisable()
    {
        zones.Remove(this);
    }

    private void Awake()
    {
        if (healPowerBar != null && maxHealPower > 0)
        {
            healPowerBar.fillAmount = 1f;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            HealPower.OnValueChanged += HandleHealPowerChanged;
            HandleHealPowerChanged(0, HealPower.Value);
        }

        if (IsServer)
        {
            HealPower.Value = maxHealPower;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            HealPower.OnValueChanged -= HandleHealPowerChanged;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsServer) { return; }

        if (!col.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) { return; }

        if (playersInZone.Add(player))
        {
            Debug.Log($"Entered: {player.PlayerName.Value}");
        }
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (!IsServer) { return; }

        if (!col.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) { return; }

        if (playersInZone.Remove(player))
        {
            Debug.Log($"Left: {player.PlayerName.Value}");
        }
    }

    private void Update()
    {
        if (!IsServer) { return; }
        if (healTickRate <= 0f) { return; }

        if (remainingCooldown > 0f)
        {
            remainingCooldown -= Time.deltaTime;
            if (remainingCooldown <= 0f)
            {
                HealPower.Value = maxHealPower;
            }
            else
            {
                return;
            }
        }

        float tickInterval = 1f / healTickRate;
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            foreach (TankPlayer player in playersInZone)
            {
                if (player == null || player.Health == null || player.Wallet == null) { continue; }
                if (HealPower.Value == 0) { break; }
                if (player.Health.MauHienTai.Value == player.Health.MauToiDa) { continue; }
                if (player.Wallet.TotalCoins.Value < coinsPerTick) { continue; }

                player.Wallet.SpendCoins(coinsPerTick);
                player.Health.HoiMau(healthPerTick);

                HealPower.Value -= 1;

                if (HealPower.Value == 0)
                {
                    remainingCooldown = healCooldown;
                }
            }

            tickTimer = tickTimer % tickInterval;
        }
    }

    private void HandleHealPowerChanged(int oldHealPower, int newHealPower)
    {
        if (healPowerBar == null || maxHealPower <= 0) { return; }
        healPowerBar.fillAmount = (float)newHealPower / maxHealPower;
    }
}
