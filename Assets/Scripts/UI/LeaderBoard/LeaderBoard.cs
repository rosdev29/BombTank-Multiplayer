using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;
    [SerializeField] private Transform teamLeaderboardEntityHolder;
    [SerializeField] private GameObject teamLeaderboardBackground;
    [SerializeField] private LeaderBoardEntityDisplay leaderboardEntityPrefab;
    [SerializeField] private Color ownerColour;
    [SerializeField] private string[] teamNames;
    [SerializeField] private TeamColourLookup teamColourLookup;
    [SerializeField] private float leaderboardRowSpacing = 28f;
    [SerializeField] private float staleEntryCleanupInterval = 0.5f;

    private NetworkList<LeaderboardEntityState> leaderboardEntities;
    private List<LeaderBoardEntityDisplay> entityDisplays = new List<LeaderBoardEntityDisplay>();
    private List<LeaderBoardEntityDisplay> teamEntityDisplays = new List<LeaderBoardEntityDisplay>();
    private readonly Dictionary<ulong, CoinChangedSubscription> coinChangedSubscriptions =
        new Dictionary<ulong, CoinChangedSubscription>();
    private bool isTearingDown;
    private float staleCleanupTimer;

    private void Awake()
    {
        leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    }

    public override void OnNetworkSpawn()
    {
        isTearingDown = false;
        staleCleanupTimer = 0f;

        if (IsClient)
        {
            if (ClientSingleton.Instance != null &&
                ClientSingleton.Instance.GameManager != null &&
                ClientSingleton.Instance.GameManager.UserData != null &&
                ClientSingleton.Instance.GameManager.UserData.userGamePreferences != null &&
                ClientSingleton.Instance.GameManager.UserData.userGamePreferences.gameQueue == GameQueue.Team &&
                teamLeaderboardBackground != null &&
                teamLeaderboardEntityHolder != null &&
                leaderboardEntityPrefab != null)
            {
                teamLeaderboardBackground.SetActive(true);

                for (int i = 0; i < teamNames.Length; i++)
                {
                    LeaderBoardEntityDisplay teamLeaderboardEntity =
                        Instantiate(leaderboardEntityPrefab, teamLeaderboardEntityHolder);

                    teamLeaderboardEntity.Initialise(i, teamNames[i], 0);

                    if (teamColourLookup != null)
                    {
                        Color teamColour = teamColourLookup.GetTeamColour(i);
                        teamLeaderboardEntity.SetColour(teamColour);
                    }

                    teamEntityDisplays.Add(teamLeaderboardEntity);
                }
            }

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
            if (NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnectedServer;
            }
        }
    }

    private void Update()
    {
        if (!IsServer || isTearingDown) { return; }
        if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) { return; }
        if (leaderboardEntities == null || leaderboardEntities.Count == 0) { return; }

        staleCleanupTimer += Time.unscaledDeltaTime;
        if (staleCleanupTimer < staleEntryCleanupInterval) { return; }

        staleCleanupTimer = 0f;
        RemoveStaleEntries();
    }

    public override void OnNetworkDespawn()
    {
        isTearingDown = true;

        if (leaderboardEntities != null)
        {
            leaderboardEntities.OnListChanged -= HandleLeaderboardEntitiesChanged;
        }

        TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnectedServer;
        }
    }

    private void HandleLeaderboardEntitiesChanged(NetworkListEvent<LeaderboardEntityState> changeEvent)
    {
        if (!gameObject.scene.isLoaded) { return; }
        if (isTearingDown) { return; }

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
                    if (NetworkManager.Singleton.LocalClientId == changeEvent.Value.ClientId)
                    {
                        Color localColour = ownerColour.a > 0.01f ? ownerColour : Color.red;
                        leaderboardEntity.SetColour(localColour);
                    }
                    entityDisplays.Add(leaderboardEntity);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                LeaderBoardEntityDisplay displayToRemove =
                    entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (displayToRemove != null)
                {
                    displayToRemove.transform.SetParent(null);
                    Destroy(displayToRemove.gameObject);
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
        
        entityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

        for (int i = 0; i < entityDisplays.Count; i++)
        {
            entityDisplays[i].transform.SetSiblingIndex(i);
            entityDisplays[i].UpdateText();
            entityDisplays[i].gameObject.SetActive(true);
        }
        EnsureVisibleStack(leaderboardEntityHolder, entityDisplays);

        if (teamLeaderboardBackground == null || !teamLeaderboardBackground.activeSelf) { return; }

        LeaderBoardEntityDisplay teamDisplay =
            teamEntityDisplays.FirstOrDefault(x => x.TeamIndex == changeEvent.Value.TeamIndex);

        if (teamDisplay != null)
        {
            if (changeEvent.Type == NetworkListEvent<LeaderboardEntityState>.EventType.Remove)
            {
                teamDisplay.UpdateCoins(teamDisplay.Coins - changeEvent.Value.Coins);
            }
            else
            {
                teamDisplay.UpdateCoins(
                    teamDisplay.Coins + (changeEvent.Value.Coins - changeEvent.PreviousValue.Coins));
            }

            teamEntityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

            for (int i = 0; i < teamEntityDisplays.Count; i++)
            {
                teamEntityDisplays[i].transform.SetSiblingIndex(i);
                teamEntityDisplays[i].UpdateText();
            }
            EnsureVisibleStack(teamLeaderboardEntityHolder, teamEntityDisplays);
        }
    }

    private void EnsureVisibleStack(Transform holder, List<LeaderBoardEntityDisplay> displays)
    {
        if (holder == null || displays == null || displays.Count == 0) { return; }

        // If a layout group exists, Unity handles positioning.
        if (holder.GetComponent<LayoutGroup>() != null) { return; }

        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] == null) { continue; }
            RectTransform rt = displays[i].transform as RectTransform;
            if (rt == null) { continue; }
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * leaderboardRowSpacing);
        }
    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        if (isTearingDown) { return; }
        if (!IsServer || !IsSpawned || leaderboardEntities == null || player == null) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }
        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId == player.OwnerClientId)
            {
                return;
            }
        }

        leaderboardEntities.Add(new LeaderboardEntityState
        {
            ClientId = player.OwnerClientId,
            PlayerName = player.PlayerName.Value,
            TeamIndex = player.TeamIndex.Value,
            Coins = 0
        });

        if (player.Wallet != null && !coinChangedSubscriptions.ContainsKey(player.OwnerClientId))
        {
            NetworkVariable<int>.OnValueChangedDelegate handler =
                (oldCoins, newCoins) => HandleCoinsChanged(player.OwnerClientId, newCoins);
            coinChangedSubscriptions[player.OwnerClientId] = new CoinChangedSubscription
            {
                wallet = player.Wallet,
                handler = handler
            };
            player.Wallet.TotalCoins.OnValueChanged += handler;
        }
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (isTearingDown) { return; }
        if (player == null) { return; }
        if (!IsServer || leaderboardEntities == null) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }

        RemoveLeaderboardEntity(player.OwnerClientId);
        UnsubscribeCoinHandler(player);
    }

    private void HandleCoinsChanged(ulong clientId, int newCoins)
    {
        if (isTearingDown || !IsServer || !IsSpawned || leaderboardEntities == null) { return; }

        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId != clientId) { continue; }

            leaderboardEntities[i] = new LeaderboardEntityState
            {
                ClientId = leaderboardEntities[i].ClientId,
                PlayerName = leaderboardEntities[i].PlayerName,
                TeamIndex = leaderboardEntities[i].TeamIndex,
                Coins = newCoins
            };

            return;
        }
    }

    private void HandleClientDisconnectedServer(ulong clientId)
    {
        if (isTearingDown) { return; }
        RemoveLeaderboardEntity(clientId);
        UnsubscribeCoinHandlerByClientId(clientId);
    }

    private void RemoveLeaderboardEntity(ulong clientId)
    {
        if (leaderboardEntities == null) { return; }

        for (int i = leaderboardEntities.Count - 1; i >= 0; i--)
        {
            LeaderboardEntityState entity = leaderboardEntities[i];
            if (entity.ClientId != clientId) { continue; }

            try
            {
                leaderboardEntities.RemoveAt(i);
            }
            catch (NullReferenceException)
            {
                // Netcode can tear down NetworkVariables before this callback runs during shutdown.
                isTearingDown = true;
            }
        }
    }

    private void UnsubscribeCoinHandler(TankPlayer player)
    {
        if (player == null) { return; }
        UnsubscribeCoinHandlerByClientId(player.OwnerClientId);
    }

    private void UnsubscribeCoinHandlerByClientId(ulong clientId)
    {
        CoinChangedSubscription subscription;
        if (!coinChangedSubscriptions.TryGetValue(clientId, out subscription))
        {
            return;
        }

        if (subscription.wallet != null)
        {
            subscription.wallet.TotalCoins.OnValueChanged -= subscription.handler;
        }

        coinChangedSubscriptions.Remove(clientId);
    }

    private void RemoveStaleEntries()
    {
        HashSet<ulong> activePlayerIds = new HashSet<ulong>();
        TankPlayer[] activePlayers = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < activePlayers.Length; i++)
        {
            TankPlayer activePlayer = activePlayers[i];
            if (activePlayer == null || !activePlayer.IsSpawned) { continue; }
            activePlayerIds.Add(activePlayer.OwnerClientId);
        }

        for (int i = leaderboardEntities.Count - 1; i >= 0; i--)
        {
            ulong clientId = leaderboardEntities[i].ClientId;
            bool isConnected = IsClientStillConnected(clientId);
            bool hasActivePlayer = activePlayerIds.Contains(clientId);
            if (isConnected && hasActivePlayer) { continue; }

            RemoveLeaderboardEntity(clientId);
            UnsubscribeCoinHandlerByClientId(clientId);
        }
    }

    private bool IsClientStillConnected(ulong clientId)
    {
        if (NetworkManager == null) { return false; }
        if (clientId == NetworkManager.ServerClientId) { return true; }
        if (NetworkManager.ConnectedClients == null) { return false; }

        return NetworkManager.ConnectedClients.ContainsKey(clientId);
    }

    private sealed class CoinChangedSubscription
    {
        public CoinWallet wallet;
        public NetworkVariable<int>.OnValueChangedDelegate handler;
    }
}
