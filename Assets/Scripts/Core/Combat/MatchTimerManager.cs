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
        MatchEndBridge.Reset();

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
        if (!IsSpawned || !IsServer || matchEnded) { return; }
        if (NetworkManager == null || NetworkManager.ShutdownInProgress) { return; }
        if (!NetworkManager.IsListening) { return; }

        float next = Mathf.Max(0f, timeRemaining.Value - Time.deltaTime);
        timeRemaining.Value = next;

        if (next <= 0f)
        {
            EndMatch();
        }
    }

    private void EndMatch()
    {
        if (matchEnded) { return; }
        matchEnded = true;

        MatchEndBridge.NotifyMatchEnded();
        FreezeAllTanks();
        NotifyMatchEndedClientRpc();
    }

    private void FreezeAllTanks()
    {
        TankPlayer[] tanks = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < tanks.Length; i++)
        {
            TankPlayer tank = tanks[i];
            if (tank == null) { continue; }

            BotBrain brain = tank.GetComponent<BotBrain>();
            if (brain != null)
            {
                brain.enabled = false;
            }

            if (tank.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    [ClientRpc]
    private void NotifyMatchEndedClientRpc()
    {
        MatchEndBridge.NotifyMatchEnded();

        bool isWinner = false;

        LeaderBoardEntityDisplay[] displays =
            FindObjectsByType<LeaderBoardEntityDisplay>(FindObjectsSortMode.None);

        if (displays != null && displays.Length > 0)
        {
            System.Array.Sort(displays,
                (a, b) => b.Coins.CompareTo(a.Coins));

            if (NetworkManager.Singleton != null)
            {
                ulong localClientId = NetworkManager.Singleton.LocalClientId;

                isWinner = displays[0].ClientId == localClientId;

                Debug.Log($"Top Player ID: {displays[0].ClientId}");
                Debug.Log($"Local Player ID: {localClientId}");
                Debug.Log($"Is Winner: {isWinner}");
            }
        }

        if (AudioManager.Instance != null)
        {
            if (isWinner)
            {
                AudioManager.Instance.PlayWinMusic();
            }
            else
            {
                AudioManager.Instance.PlayLoseMusic();
            }
        }

        MatchEndClient.ShowEndOverlay();
    }
}