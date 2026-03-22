using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerGameManager : IDisposable
{
    private string serverIP;
    private int serverPort;
    private int queryPort;
    private NetworkServer networkServer;
    private MultiplayAllocationService multiplayAllocationService;

    private const string GameSceneName = "Game";

    public ServerGameManager(string serverIP, int serverPort, int queryPort, NetworkManager manager)
    {
        this.serverIP = serverIP;
        this.serverPort = serverPort;
        this.queryPort = queryPort;
        networkServer = new NetworkServer(manager);
        multiplayAllocationService = new MultiplayAllocationService();
    }

    public async Task StartGameServerAsync()
    {
        await multiplayAllocationService.BeginServerCheck();

        if (!networkServer.OpenConnection(serverIP, serverPort))
        {
            Debug.LogWarning("NetworkServer did not start as expected.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    public void Dispose()
    {
        multiplayAllocationService?.Dispose();
        networkServer?.Dispose();
    }
}
