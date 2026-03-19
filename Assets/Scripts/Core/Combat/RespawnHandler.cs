using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private TankPlayer playerPrefab;
    [SerializeField] private float keptCoinPercentage;

    // Track per-player death handler so we can unsubscribe cleanly.
    private readonly Dictionary<TankPlayer, Action<Mau>> dieHandlersByPlayer = new Dictionary<TankPlayer, Action<Mau>>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in players)
        {
            HandlePlayerSpawned(player);
        }

        TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;

        foreach (var kvp in dieHandlersByPlayer)
        {
            if (kvp.Key != null && kvp.Key.Health != null)
            {
                kvp.Key.Health.KhiChet -= kvp.Value;
            }
        }

        dieHandlersByPlayer.Clear();
    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        if (player == null) { return; }
        if (player.Health == null) { return; }
        if (dieHandlersByPlayer.ContainsKey(player)) { return; }

        Action<Mau> handler = _ => HandlePlayerDie(player);
        dieHandlersByPlayer[player] = handler;
        player.Health.KhiChet += handler;
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (player == null) { return; }
        if (player.Health == null) { return; }

        if (dieHandlersByPlayer.TryGetValue(player, out var handler))
        {
            player.Health.KhiChet -= handler;
            dieHandlersByPlayer.Remove(player);
        }
    }

    private void HandlePlayerDie(TankPlayer player)
    {
        if (player == null) { return; }
        int keptCoins = (int)(player.Wallet.TotalCoins.Value * (keptCoinPercentage / 100));

        HandlePlayerDespawned(player);

        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayer(player.OwnerClientId, keptCoins));
    }

    private IEnumerator RespawnPlayer(ulong ownerClientId, int keptCoins)
    {
        // Wait one frame to allow despawn to complete.
        yield return null;

        TankPlayer playerInstance = Instantiate(
            playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);

        playerInstance.NetworkObject.SpawnAsPlayerObject(ownerClientId);
        playerInstance.Wallet.TotalCoins.Value += keptCoins;
    }
}
