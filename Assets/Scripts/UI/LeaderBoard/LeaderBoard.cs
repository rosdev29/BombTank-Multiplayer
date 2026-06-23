using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
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
    [SerializeField] private float pingRefreshInterval = 0.5f;

    private NetworkList<LeaderboardEntityState> leaderboardEntities;
    private List<LeaderBoardEntityDisplay> entityDisplays = new List<LeaderBoardEntityDisplay>();
    private List<LeaderBoardEntityDisplay> teamEntityDisplays = new List<LeaderBoardEntityDisplay>();

    private readonly Dictionary<ulong, CoinChangedSubscription> coinChangedSubscriptions =
        new Dictionary<ulong, CoinChangedSubscription>();

    private bool isTearingDown;
    private float staleCleanupTimer;
    private float pingRefreshTimer;
    private bool leaderboardUiSetup;

    private void Awake()
    {
        leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    }

    public override void OnNetworkSpawn()
    {
        isTearingDown = false;
        staleCleanupTimer = 0f;
        pingRefreshTimer = 0f;

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
                ConfigureLeaderboardHolder(teamLeaderboardEntityHolder);

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

        if (IsClient)
        {
            SetupLeaderboardPresentation();
            BountySystem.OnCrownListChanged += RefreshAllDisplayTexts;
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

        pingRefreshTimer += Time.unscaledDeltaTime;
        if (pingRefreshTimer >= pingRefreshInterval)
        {
            pingRefreshTimer = 0f;
            RefreshPingValues();
        }

        staleCleanupTimer += Time.unscaledDeltaTime;
        if (staleCleanupTimer < staleEntryCleanupInterval) { return; }

        staleCleanupTimer = 0f;
        RemoveStaleEntries();
    }

    private void RefreshAllDisplayTexts()
    {
        foreach (LeaderBoardEntityDisplay display in entityDisplays)
        {
            if (display != null) { display.UpdateText(); }
        }
    }

    public override void OnNetworkDespawn()
    {
        isTearingDown = true;

        BountySystem.OnCrownListChanged -= RefreshAllDisplayTexts;

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

    private ulong ResolveCrownLookupId(ulong leaderboardId)
    {
        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in players)
        {
            if (player != null && GetLeaderboardId(player) == leaderboardId)
            {
                return player.NetworkObjectId;
            }
        }

        return leaderboardId;
    }

    private ulong GetLeaderboardId(TankPlayer player)
    {
        if (player != null && player.IsCurrentlyBot())
        {
            return player.NetworkObjectId;
        }

        return player.OwnerClientId;
    }

    private FixedString32Bytes GetLeaderboardName(TankPlayer player)
    {
        if (player == null)
        {
            return "Unknown";
        }

        string playerName = player.PlayerName.Value.ToString();

        if (player.IsCurrentlyBot())
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = $"Bot {player.NetworkObjectId}";
            }

            return $"{playerName} [BOT]";
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, $"Player {player.OwnerClientId}");
        }

        return playerName;
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

                    ulong crownLookupId = ResolveCrownLookupId(changeEvent.Value.ClientId);

                    leaderboardEntity.Initialise(
                        changeEvent.Value.ClientId,
                        changeEvent.Value.PlayerName,
                        changeEvent.Value.Coins,
                        crownLookupId,
                        changeEvent.Value.PingMs);

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
                    displayToUpdate.UpdateName(changeEvent.Value.PlayerName);
                    displayToUpdate.UpdateCoins(changeEvent.Value.Coins);
                    displayToUpdate.UpdatePing(changeEvent.Value.PingMs);
                }
                break;
        }

        entityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

        for (int i = 0; i < entityDisplays.Count; i++)
        {
            entityDisplays[i].SetRank(i + 1);
            entityDisplays[i].transform.SetSiblingIndex(i);
            entityDisplays[i].UpdateText();
            entityDisplays[i].gameObject.SetActive(true);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(leaderboardEntityHolder as RectTransform);

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
                teamEntityDisplays[i].SetRank(i + 1);
                teamEntityDisplays[i].transform.SetSiblingIndex(i);
                teamEntityDisplays[i].UpdateText();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(teamLeaderboardEntityHolder as RectTransform);
        }
    }

    private void SetupLeaderboardPresentation()
    {
        if (leaderboardUiSetup || leaderboardEntityHolder == null) { return; }

        leaderboardUiSetup = true;
        ConfigureLeaderboardHolder(leaderboardEntityHolder);
    }

    private void ConfigureLeaderboardHolder(Transform holderTransform)
    {
        if (holderTransform == null) { return; }

        RectTransform holder = holderTransform as RectTransform;
        if (holder == null) { return; }

        RectTransform background = holder.parent as RectTransform;
        if (background == null) { return; }

        if (background.GetComponent<ScrollRect>() == null)
        {
            ScrollRect scroll = background.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            GameObject viewportObject = new GameObject("LeaderboardViewport", typeof(RectTransform));
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(background, false);
            viewport.SetAsFirstSibling();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewportObject.AddComponent<RectMask2D>();

            holder.SetParent(viewport, false);
            holder.anchorMin = new Vector2(0f, 1f);
            holder.anchorMax = new Vector2(1f, 1f);
            holder.pivot = new Vector2(0.5f, 1f);
            holder.anchoredPosition = Vector2.zero;
            holder.sizeDelta = new Vector2(0f, 0f);

            scroll.viewport = viewport;
            scroll.content = holder;
        }
        else if (background.GetComponent<RectMask2D>() == null)
        {
            background.gameObject.AddComponent<RectMask2D>();
        }

        VerticalLayoutGroup layout = holder.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 6, 6);
        }

        ContentSizeFitter fitter = holder.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void EnsureVisibleStack(Transform holder, List<LeaderBoardEntityDisplay> displays)
    {
        if (holder == null || displays == null || displays.Count == 0) { return; }
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
        ulong leaderboardId = GetLeaderboardId(player);
        FixedString32Bytes leaderboardName = GetLeaderboardName(player);

        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId == leaderboardId)
            {
                int score = player.Wallet != null ? player.Wallet.LifetimeCoins.Value : leaderboardEntities[i].Coins;

                leaderboardEntities[i] = new LeaderboardEntityState
                {
                    ClientId = leaderboardId,
                    PlayerName = leaderboardName,
                    TeamIndex = player.TeamIndex.Value,
                    Coins = score,
                    PingMs = GetPingForLeaderboardClient(leaderboardId)
                };

                if (IsClient)
                {
                    LeaderBoardEntityDisplay display =
                        entityDisplays.FirstOrDefault(x => x.ClientId == leaderboardId);

                    if (display != null)
                    {
                        display.SetCrownLookupId(player.NetworkObjectId);
                    }
                }

                return;
            }
        }

        leaderboardEntities.Add(new LeaderboardEntityState
        {
            ClientId = leaderboardId,
            PlayerName = leaderboardName,
            TeamIndex = player.TeamIndex.Value,
            Coins = player.Wallet != null ? player.Wallet.LifetimeCoins.Value : 0,
            PingMs = GetPingForLeaderboardClient(leaderboardId)
        });

        if (IsClient)
        {
            LeaderBoardEntityDisplay display =
                entityDisplays.FirstOrDefault(x => x.ClientId == leaderboardId);

            if (display != null)
            {
                display.SetCrownLookupId(player.NetworkObjectId);
            }
        }

        if (player.Wallet != null && !coinChangedSubscriptions.ContainsKey(leaderboardId))
        {
            NetworkVariable<int>.OnValueChangedDelegate handler =
                (oldCoins, newCoins) => HandleCoinsChanged(leaderboardId, newCoins);

            coinChangedSubscriptions[leaderboardId] = new CoinChangedSubscription
            {
                wallet = player.Wallet,
                handler = handler
            };

            player.Wallet.LifetimeCoins.OnValueChanged += handler;
        }
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (isTearingDown) { return; }
        if (player == null) { return; }
        if (!IsServer || leaderboardEntities == null) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }

        ulong leaderboardId = GetLeaderboardId(player);

        RemoveLeaderboardEntity(leaderboardId);
        UnsubscribeCoinHandlerByClientId(leaderboardId);
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
                Coins = newCoins,
                PingMs = leaderboardEntities[i].PingMs
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

                isTearingDown = true;
            }
        }
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
            subscription.wallet.LifetimeCoins.OnValueChanged -= subscription.handler;
        }

        coinChangedSubscriptions.Remove(clientId);
    }

    private void RemoveStaleEntries()
    {
        HashSet<ulong> activeLeaderboardIds = new HashSet<ulong>();

        TankPlayer[] activePlayers = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);

        for (int i = 0; i < activePlayers.Length; i++)
        {
            TankPlayer activePlayer = activePlayers[i];

            if (activePlayer == null || !activePlayer.IsSpawned) { continue; }

            activeLeaderboardIds.Add(GetLeaderboardId(activePlayer));
        }

        for (int i = leaderboardEntities.Count - 1; i >= 0; i--)
        {
            ulong clientId = leaderboardEntities[i].ClientId;

            bool hasActivePlayer = activeLeaderboardIds.Contains(clientId);
            bool isConnectedClient = IsClientStillConnected(clientId);

            if (hasActivePlayer || isConnectedClient) { continue; }

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

    private bool IsHumanLeaderboardClient(ulong clientId)
    {
        if (NetworkManager == null || NetworkManager.ConnectedClients == null) { return false; }
        return NetworkManager.ConnectedClients.ContainsKey(clientId);
    }

    private int GetPingForLeaderboardClient(ulong clientId)
    {
        if (!IsHumanLeaderboardClient(clientId)) { return -1; }

        NetworkTransport transport = NetworkManager?.NetworkConfig?.NetworkTransport;
        if (transport == null) { return -1; }

        return (int)transport.GetCurrentRtt(clientId);
    }

    private void RefreshPingValues()
    {
        if (!IsServer || leaderboardEntities == null) { return; }

        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            LeaderboardEntityState entity = leaderboardEntities[i];
            int pingMs = GetPingForLeaderboardClient(entity.ClientId);

            if (entity.PingMs == pingMs) { continue; }

            leaderboardEntities[i] = new LeaderboardEntityState
            {
                ClientId = entity.ClientId,
                PlayerName = entity.PlayerName,
                TeamIndex = entity.TeamIndex,
                Coins = entity.Coins,
                PingMs = pingMs
            };
        }
    }

    private sealed class CoinChangedSubscription
    {
        public CoinWallet wallet;
        public NetworkVariable<int>.OnValueChangedDelegate handler;
    }
}
