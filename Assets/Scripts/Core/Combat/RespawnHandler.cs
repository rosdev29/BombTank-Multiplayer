using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private TankPlayer playerPrefab;
    [SerializeField] private float respawnDelaySeconds = 5f;

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
        if (player.IsCurrentlyBot()) { return; }

        ulong ownerClientId = player.OwnerClientId;

        NotifyDeathClientRpc(
            respawnDelaySeconds,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { ownerClientId }
                }
            });

        HandlePlayerDespawned(player);

        // Wait one frame so KillFeedManager (and other KhiChet listeners) run before despawn unsubscribes them.
        StartCoroutine(DespawnAndRespawnAfterDeath(player, ownerClientId));
    }

    private IEnumerator DespawnAndRespawnAfterDeath(TankPlayer player, ulong ownerClientId)
    {
        yield return null;

        int respawnCoins = 0;
        int lifetimeScore = 0;
        if (player != null && player.Wallet != null)
        {
            respawnCoins = player.Wallet.ProcessDeathCoinDrop();
            lifetimeScore = player.Wallet.LifetimeCoins.Value;
        }

        if (player != null)
        {
            DespawnPlayerObject(player);
        }

        yield return new WaitForSeconds(respawnDelaySeconds);
        yield return RespawnPlayer(ownerClientId, respawnCoins, lifetimeScore);
    }

    private static void DespawnPlayerObject(TankPlayer player)
    {
        if (player == null) { return; }

        NetworkObject netObj = player.NetworkObject;
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
            return;
        }

        Destroy(player.gameObject);
    }

    private IEnumerator RespawnPlayer(ulong ownerClientId, int respawnCoins, int lifetimeScore)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.ShutdownInProgress)
        {
            yield break;
        }

        if (!networkManager.ConnectedClients.ContainsKey(ownerClientId))
        {
            yield break;
        }

        // NGO may still track a player object for this client — remove it before respawning.
        NetworkObject stalePlayer = networkManager.SpawnManager.GetPlayerNetworkObject(ownerClientId);
        if (stalePlayer != null && stalePlayer.IsSpawned)
        {
            stalePlayer.Despawn(true);
            yield return null;
        }

        TankPlayer playerInstance = Instantiate(
            playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);

        playerInstance.NetworkObject.SpawnAsPlayerObject(ownerClientId);
        playerInstance.Wallet.TotalCoins.Value = respawnCoins;
        playerInstance.Wallet.LifetimeCoins.Value = lifetimeScore;
    }

    [ClientRpc]
    private void NotifyDeathClientRpc(float delaySeconds, ClientRpcParams rpcParams = default)
    {
        DeathSpectatorClient.NotifyDeath(delaySeconds);
    }
}
