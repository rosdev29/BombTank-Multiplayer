using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkServer : IDisposable
{
    private NetworkManager networkManager;
    private NetworkObject playerPrefab;
    public Action<UserData> OnUserJoined;
    public Action<UserData> OnUserLeft;
    public Action<string> OnClientLeft;
    private Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> previousApprovalCallback;

    private Dictionary<ulong, string> clientIdToAuth = new Dictionary<ulong, string>();
    private Dictionary<string, UserData> authIdToUserData = new Dictionary<string, UserData>();
    private Dictionary<ulong, int> clientSpawnPointIndices = new Dictionary<ulong, int>();

    public NetworkServer(NetworkManager networkManager, NetworkObject playerPrefab = null)
    {
        this.networkManager = networkManager;
        this.playerPrefab = playerPrefab;

        previousApprovalCallback = networkManager.ConnectionApprovalCallback;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;
        networkManager.OnServerStarted += OnNetworkReady;
    }

    public bool OpenConnection(string ip, int port)
    {
        UnityTransport transport = networkManager.gameObject.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, (ushort)port);
        return networkManager.StartServer();
    }

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        UserData userData = GetUserDataFromPayload(request.Payload, request.ClientNetworkId);

        Debug.Log(userData.userName);

        clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
        authIdToUserData[userData.userAuthId] = userData;
        OnUserJoined?.Invoke(userData);

        if (playerPrefab == null && networkManager.NetworkConfig != null && networkManager.NetworkConfig.PlayerPrefab != null)
        {
            playerPrefab = networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>();
        }

        Vector3 spawnPosition = GetSpawnPositionForClient(request.ClientNetworkId);

        if (playerPrefab != null)
        {
            _ = SpawnPlayerDelayed(request.ClientNetworkId, spawnPosition);
        }

        response.Approved = true;
        response.Position = spawnPosition;
        response.Rotation = Quaternion.identity;
        response.CreatePlayerObject = playerPrefab == null;
    }

    private async Task SpawnPlayerDelayed(ulong clientId, Vector3 spawnPosition)
    {
        await Task.Delay(1000);

        if (playerPrefab == null || networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        NetworkObject playerInstance =
            GameObject.Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        playerInstance.SpawnAsPlayerObject(clientId);
    }

    private void OnNetworkReady()
    {
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            if (authIdToUserData.TryGetValue(authId, out UserData userData))
            {
                OnUserLeft?.Invoke(userData);
            }
            OnClientLeft?.Invoke(authId);
            clientIdToAuth.Remove(clientId);
            authIdToUserData.Remove(authId);
        }

        clientSpawnPointIndices.Remove(clientId);
    }

    public UserData GetUserDataByClientId(ulong clientId)
    {
        if (clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            if (authIdToUserData.TryGetValue(authId, out UserData data))
            {
                return data;
            }

            return null;
        }

        return null;
    }

    public void Dispose()
    {
        if (networkManager == null) { return; }

        if (networkManager.ConnectionApprovalCallback == ApprovalCheck)
        {
            networkManager.ConnectionApprovalCallback = previousApprovalCallback;
        }
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnServerStarted -= OnNetworkReady;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }

    private static UserData GetUserDataFromPayload(byte[] payloadBytes, ulong clientId)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
        {
            return CreateFallbackUserData(clientId);
        }

        string payload = System.Text.Encoding.UTF8.GetString(payloadBytes);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return CreateFallbackUserData(clientId);
        }

        UserData userData = JsonUtility.FromJson<UserData>(payload);
        if (userData == null)
        {
            return CreateFallbackUserData(clientId);
        }

        if (string.IsNullOrWhiteSpace(userData.userName))
        {
            userData.userName = $"Player {clientId}";
        }

        if (string.IsNullOrWhiteSpace(userData.userAuthId))
        {
            userData.userAuthId = $"lan-{clientId}";
        }

        if (userData.userGamePreferences == null)
        {
            userData.userGamePreferences = new GameInfo();
        }

        return userData;
    }

    private static UserData CreateFallbackUserData(ulong clientId)
    {
        return new UserData
        {
            userName = $"Player {clientId}",
            userAuthId = $"lan-{clientId}",
            teamIndex = -1,
            userGamePreferences = new GameInfo()
        };
    }

    private Vector3 GetSpawnPositionForClient(ulong clientId)
    {
        SpawnPoint[] spawnPoints = UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            clientSpawnPointIndices[clientId] = -1;
            return SpawnPoint.GetRandomSpawnPos();
        }

        HashSet<int> usedIndices = clientSpawnPointIndices.Values.Where(index => index >= 0).ToHashSet();
        int startIndex = UnityEngine.Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int candidate = (startIndex + i) % spawnPoints.Length;
            if (usedIndices.Contains(candidate)) { continue; }

            clientSpawnPointIndices[clientId] = candidate;
            return spawnPoints[candidate].transform.position;
        }

        int fallbackIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
        clientSpawnPointIndices[clientId] = fallbackIndex;
        return spawnPoints[fallbackIndex].transform.position;
    }
}
