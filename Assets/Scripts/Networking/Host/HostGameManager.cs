using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Networking.Transport.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
public class HostGameManager : IDisposable
{
    private Allocation allocation;
    private string joinCode;
    private string lobbyId;
    private RelayServerData relayServerData;
    private bool hasRelayServerData;
    private bool isStartingHost;
    private static bool isAnyHostStartInFlight;

    public NetworkServer NetworkServer { get; private set; }
    private const int MaxConnections = 20;
    private const string GameScenceName = "Game";

    public RelayServerData RelayServerData => relayServerData;
    public bool HasRelayServerData => hasRelayServerData;

    public async Task StartHostAsync()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null) { return; }

        if (isStartingHost || isAnyHostStartInFlight)
        {
            Debug.LogWarning("StartHostAsync is already running.");
            return;
        }

        if (networkManager.IsListening ||
            networkManager.IsServer ||
            networkManager.IsClient ||
            networkManager.ShutdownInProgress)
        {
            Debug.LogWarning("NetworkManager is already running. Skip StartHost.");
            return;
        }

        isStartingHost = true;
        isAnyHostStartInFlight = true;
        try
        {
            try
            {
                allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
                relayServerData = allocation.ToRelayServerData("dtls");
                hasRelayServerData = true;
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            try
            {
                joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log(joinCode);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }

            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            if (transport != null && hasRelayServerData)
                transport.SetRelayServerData(relayServerData);

            try
            {
                var lobbyOptions = new CreateLobbyOptions();
                lobbyOptions.IsPrivate = false;
                lobbyOptions.Data = new Dictionary<string, DataObject>
                {
                    {
                        "JoinCode",
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: joinCode)
                    }
                };

                string playerName = PlayerPrefs.GetString("PlayerName", "Unknown");

                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                    $"{playerName}'s Lobby", MaxConnections, lobbyOptions);

                lobbyId = lobby.Id;
                Debug.Log($"Lobby created. LobbyId={lobbyId}");

                HostSingleton.Instance.StartCoroutine(HearbeatLobby(15f));
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
                return;
            }

            if (networkManager.IsListening ||
                networkManager.IsServer ||
                networkManager.IsClient ||
                networkManager.ShutdownInProgress)
            {
                return;
            }

            // If host is started multiple times (e.g. user clicks twice), ensure we don't double-register callbacks.
            NetworkServer?.Dispose();
            NetworkServer = new NetworkServer(networkManager);

            UserData userData = new UserData
            {
                userName = PlayerPrefs.GetString("PlayerName", "Missing Name"),
                userAuthId = AuthenticationService.Instance.PlayerId
            };

            string payload = JsonUtility.ToJson(userData);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            networkManager.NetworkConfig.ConnectionData = payloadBytes;

            if (!networkManager.StartHost())
            {
                Debug.LogWarning("StartHost failed because NetworkManager cannot start now.");
                return;
            }

            networkManager.SceneManager.LoadScene(GameScenceName, LoadSceneMode.Single);
        }
        finally
        {
            isStartingHost = false;
            isAnyHostStartInFlight = false;
        }
    }

    private IEnumerator HearbeatLobby(float waitTimeSeconds)
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    public async void Dispose()
    {
        HostSingleton.Instance.StopCoroutine(HearbeatLobby(15f));

        if (!string.IsNullOrEmpty(lobbyId))
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }

            lobbyId = string.Empty;
        }

        NetworkServer?.Dispose();
    }
}
