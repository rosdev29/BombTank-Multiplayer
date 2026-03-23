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
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerGameManager : IDisposable
{
    private string serverIP;
    private int serverPort;
    private int queryPort;
    private MatchplayBackfiller backfiller;
    private MultiplayAllocationService multiplayAllocationService;
    private Dictionary<string, int> teamIdToTeamIndex = new Dictionary<string, int>();

    private const string GameSceneName = "Game";
    public NetworkServer NetworkServer { get; private set; }

    public ServerGameManager(string serverIP, int serverPort, int queryPort, NetworkManager manager)
    {
        this.serverIP = serverIP;
        this.serverPort = serverPort;
        this.queryPort = queryPort;
        NetworkServer = new NetworkServer(manager);
        multiplayAllocationService = new MultiplayAllocationService();
    }

    public async Task StartGameServerAsync()
    {
        await multiplayAllocationService.BeginServerCheck();

        if (!NetworkServer.OpenConnection(serverIP, serverPort))
        {
            Debug.LogWarning("NetworkServer did not start as expected.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    private void UserJoined(UserData user)
    {
        if (user == null || backfiller == null) { return; }

        Team team = backfiller.GetTeamByUserId(user.userAuthId);
        if (team == null || string.IsNullOrEmpty(team.TeamId))
        {
            user.teamIndex = 0;
        }
        else
        {
            Debug.Log($"{user.userAuthId} {team.TeamId}");
            if (!teamIdToTeamIndex.TryGetValue(team.TeamId, out int teamIndex))
            {
                teamIndex = teamIdToTeamIndex.Count;
                teamIdToTeamIndex.Add(team.TeamId, teamIndex);
            }

            user.teamIndex = teamIndex;
        }

        multiplayAllocationService.AddPlayer();
        if (!backfiller.NeedsPlayers() && backfiller.IsBackfilling)
        {
            _ = backfiller.StopBackfill();
        }
    }

    public void Dispose()
    {
        multiplayAllocationService?.Dispose();
        NetworkServer?.Dispose();
    }
}
