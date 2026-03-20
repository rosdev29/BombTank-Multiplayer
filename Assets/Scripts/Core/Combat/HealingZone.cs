using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealingZone : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Image healPowerBar;

    [Header("Settings")]
    [SerializeField] private int maxHealPower = 30;
    [SerializeField] private float healCooldown = 60f;
    [SerializeField] private float healTickRate = 1f;
    [SerializeField] private int coinsPerTick = 10;
    [SerializeField] private int healthPerTick = 10;

    private HashSet<TankPlayer> playersInZone = new HashSet<TankPlayer>();

    private void Awake()
    {
        if (healPowerBar != null && maxHealPower > 0)
        {
            healPowerBar.fillAmount = 1f;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsServer) { return; }

        if (!col.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) { return; }

        if (playersInZone.Add(player))
        {
            Debug.Log(
                $"Entered: {player.PlayerName.Value} " +
                $"(cooldown: {healCooldown}s, tickRate: {healTickRate}s, coins/tick: {coinsPerTick}, heal/tick: {healthPerTick})");
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
}
