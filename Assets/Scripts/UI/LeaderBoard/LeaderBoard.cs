using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;
    [SerializeField] private LeaderBoardEntityDisplay leaderboardEntityPrefab;

    private NetworkList<LeaderboardEntityState> leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    private readonly List<LeaderBoardEntityDisplay> entityDisplays = new List<LeaderBoardEntityDisplay>();
    private readonly Dictionary<ulong, NetworkVariable<int>.OnValueChangedDelegate> coinChangeHandlers =
        new Dictionary<ulong, NetworkVariable<int>.OnValueChangedDelegate>();

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged += HandleLeaderboardEntitiesChanged;
            foreach (LeaderboardEntityState entity in leaderboardEntities)
            {
                HandleLeaderboardEntitiesChanged(new NetworkListEvent<LeaderboardEntityState>
                {
                    Type = NetworkListEvent<LeaderboardEntityState>.EventType.Add,
                    Value = entity
                });
            }
        }

        if (IsServer)
        {
            TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
            foreach (TankPlayer player in players)
            {
                HandlePlayerSpawned(player);
            }

            TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
            TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged -= HandleLeaderboardEntitiesChanged;
        }

        if (IsServer)
        {
            TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
            TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;
        }
    }

    private void HandleLeaderboardEntitiesChanged(NetworkListEvent<LeaderboardEntityState> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<LeaderboardEntityState>.EventType.Add:
                if (!entityDisplays.Any(x => x.ClientId == changeEvent.Value.ClientId))
                {
                    LeaderBoardEntityDisplay leaderboardEntity =
                        Instantiate(leaderboardEntityPrefab, leaderboardEntityHolder);
                    leaderboardEntity.Initialise(
                        changeEvent.Value.ClientId,
                        changeEvent.Value.PlayerName,
                        changeEvent.Value.Coins);
                    entityDisplays.Add(leaderboardEntity);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                LeaderBoardEntityDisplay displayToRemove =
                    entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (displayToRemove != null)
                {
                    displayToRemove.transform.SetParent(null);
                    UnityEngine.Object.Destroy(displayToRemove.gameObject);
                    entityDisplays.Remove(displayToRemove);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Value:
                LeaderBoardEntityDisplay displayToUpdate =
                    entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (displayToUpdate != null)
                {
                    displayToUpdate.UpdateCoins(changeEvent.Value.Coins);
                }
                break;
        }
    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        if (!IsServer || !IsSpawned || leaderboardEntities == null || player == null) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }

        leaderboardEntities.Add(new LeaderboardEntityState
        {
            ClientId = player.OwnerClientId,
            PlayerName = player.PlayerName.Value,
            Coins = 0
        });

        if (player.TryGetComponent<CoinWallet>(out var wallet))
        {
            // Avoid double-subscribe for the same client
            if (!coinChangeHandlers.ContainsKey(player.OwnerClientId))
            {
                NetworkVariable<int>.OnValueChangedDelegate handler = (oldCoins, newCoins) =>
                    HandleCoinsChanged(player.OwnerClientId, newCoins);

                coinChangeHandlers[player.OwnerClientId] = handler;
                wallet.TotalCoins.OnValueChanged += handler;
            }
        }
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (!IsServer || !IsSpawned || leaderboardEntities == null || player == null) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }

        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            LeaderboardEntityState entity = leaderboardEntities[i];
            if (entity.ClientId != player.OwnerClientId) { continue; }

            leaderboardEntities.RemoveAt(i);
            break;
        }

        if (player.TryGetComponent<CoinWallet>(out var wallet))
        {
            if (coinChangeHandlers.TryGetValue(
                    player.OwnerClientId,
                    out NetworkVariable<int>.OnValueChangedDelegate handler))
            {
                wallet.TotalCoins.OnValueChanged -= handler;
                coinChangeHandlers.Remove(player.OwnerClientId);
            }
        }
    }

    private void HandleCoinsChanged(ulong clientId, int newCoins)
    {
        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId != clientId) { continue; }

            leaderboardEntities[i] = new LeaderboardEntityState
            {
                ClientId = leaderboardEntities[i].ClientId,
                PlayerName = leaderboardEntities[i].PlayerName,
                Coins = newCoins
            };

            return;
        }
    }
}
