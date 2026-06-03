using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server listens for kills and broadcasts them to all clients.
/// Attach to the same NetworkObject as RespawnHandler in Game scene.
/// </summary>
public class KillFeedManager : NetworkBehaviour
{
    private readonly Dictionary<TankPlayer, Action<Mau>> deathHandlers = new Dictionary<TankPlayer, Action<Mau>>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) { return; }

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in players)
        {
            Subscribe(player);
        }

        TankPlayer.OnPlayerSpawned += Subscribe;
        TankPlayer.OnPlayerDespawned += Unsubscribe;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        TankPlayer.OnPlayerSpawned -= Subscribe;
        TankPlayer.OnPlayerDespawned -= Unsubscribe;

        foreach (var kvp in deathHandlers)
        {
            if (kvp.Key != null && kvp.Key.Health != null)
            {
                kvp.Key.Health.KhiChet -= kvp.Value;
            }
        }

        deathHandlers.Clear();
    }

    private void Subscribe(TankPlayer player)
    {
        if (player == null || player.Health == null) { return; }
        if (deathHandlers.ContainsKey(player)) { return; }

        Action<Mau> handler = health => HandleKill(health, player);
        deathHandlers[player] = handler;
        player.Health.KhiChet += handler;
    }

    private void Unsubscribe(TankPlayer player)
    {
        if (player == null || player.Health == null) { return; }

        if (deathHandlers.TryGetValue(player, out Action<Mau> handler))
        {
            player.Health.KhiChet -= handler;
            deathHandlers.Remove(player);
        }
    }

    private void HandleKill(Mau health, TankPlayer victim)
    {
        if (!IsServer) { return; }
        if (health == null || victim == null) { return; }
        if (!health.TryLayKeGiet(out TankPlayer killer)) { return; }
        if (killer == null || killer == victim) { return; }

        string killerName = killer.PlayerName.Value.ToString();
        string victimName = victim.PlayerName.Value.ToString();
        if (string.IsNullOrWhiteSpace(killerName)) { killerName = $"Player {killer.OwnerClientId}"; }
        if (string.IsNullOrWhiteSpace(victimName)) { victimName = $"Player {victim.OwnerClientId}"; }

        BroadcastKillClientRpc(
            killerName,
            killer.IsCurrentlyBot(),
            victimName,
            victim.IsCurrentlyBot());
    }

    [ClientRpc]
    private void BroadcastKillClientRpc(
        string killerName,
        bool killerIsBot,
        string victimName,
        bool victimIsBot)
    {
        KillFeedClient.AddEntry(killerName, killerIsBot, victimName, victimIsBot);
    }
}
