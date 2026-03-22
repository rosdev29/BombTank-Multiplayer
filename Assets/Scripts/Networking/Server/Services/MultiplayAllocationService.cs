using System;
using System.Threading.Tasks;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class MultiplayAllocationService : IDisposable
{
    private bool hasWarnedMissingMultiplay;

    public MultiplayAllocationService()
    {
        LogMissingMultiplayIfNeeded();
    }

    public async Task<MatchmakingResults> SubscribeAndAwaitMatchmakerAllocation()
    {
        LogMissingMultiplayIfNeeded();
        await Task.CompletedTask;
        return null;
    }

    public async Task BeginServerCheck()
    {
        LogMissingMultiplayIfNeeded();
        await Task.CompletedTask;
    }

    public void SetServerName(string name) { }

    public void SetBuildID(string id) { }

    public void SetMaxPlayers(ushort players) { }

    public void AddPlayer() { }

    public void RemovePlayer() { }

    public void SetMap(string newMap) { }

    public void SetMode(string mode) { }

    private void LogMissingMultiplayIfNeeded()
    {
        if (hasWarnedMissingMultiplay) { return; }
        hasWarnedMissingMultiplay = true;
        Debug.LogWarning("Multiplay package is not installed. Install com.unity.services.multiplay to enable allocation/query handling.");
    }

    public void Dispose() { }
}
