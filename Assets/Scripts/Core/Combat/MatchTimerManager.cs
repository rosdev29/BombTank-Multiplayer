using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative match countdown synced to all clients.
/// Attach to the same NetworkObject as RespawnHandler in Game scene.
/// </summary>
public class MatchTimerManager : NetworkBehaviour
{
    [SerializeField] private float matchDurationSeconds = 600f;

    // Private — do not edit in Inspector (use Match Duration Seconds above).
    private readonly NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool matchEnded;

    public float TimeRemainingSeconds => timeRemaining.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        matchEnded = false;

        if (IsServer && IsSpawned)
        {
            timeRemaining.Value = Mathf.Max(1f, matchDurationSeconds);
        }

        MatchTimerClient.Register(this);
    }

    public override void OnNetworkDespawn()
    {
        MatchTimerClient.Unregister(this);
    }

    private void Update()
    {
        // Train Mode: Disable time tracking so match never ends
        /*
        if (!IsSpawned || !IsServer || matchEnded) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }
        if (!NetworkManager.IsListening) { return; }

        float next = Mathf.Max(0f, timeRemaining.Value - Time.deltaTime);
        timeRemaining.Value = next;

        if (next <= 0f)
        {
            EndMatch();
        }
        */
    }

    private void EndMatch()
    {
        if (matchEnded) { return; }
        matchEnded = true;

        MatchEndBridge.NotifyMatchEnded();
        NotifyMatchEndedClientRpc();
    }

    [ClientRpc]
    private void NotifyMatchEndedClientRpc()
    {
        MatchEndBridge.NotifyMatchEnded();
        MatchEndClient.ShowEndOverlay();
    }
}
