using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bounty System — "Săn người giàu"
/// 
/// Server-side:
///   • Theo dõi TotalCoins của mọi TankPlayer mỗi frame.
///   • Xe nào có > BountyThreshold coin → được đánh dấu "có bounty" (crown 👑).
///   • Khi một xe có crown bị giết → kẻ giết nhận thêm BountyRewardPercent% coin của nạn nhân.
/// 
/// Client-side:
///   • NetworkList<BountyEntry> sync trạng thái crown xuống tất cả client.
///   • CrownDisplayClient đọc list này để hiện/ẩn icon vương miện trên đầu xe.
/// </summary>
public class BountySystem : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Bounty Settings")]
    [Tooltip("Số coin tối thiểu để xe bị đánh dấu 'có vương miện'.")]
    [SerializeField] private int bountyThreshold = 100;

    [Tooltip("Phần trăm coin của nạn nhân mà kẻ giết nhận thêm khi giết xe có crown.")]
    [SerializeField] private float bountyRewardPercent = 20f;

    [Tooltip("Bao lâu (giây) cập nhật danh sách crown 1 lần — 0.2s là đủ mịn.")]
    [SerializeField] private float updateInterval = 0.2f;

    // ─── Network State (sync Server → tất cả Client) ─────────────────────────
    /// <summary>
    /// Set các NetworkObjectId của xe đang có crown.
    /// Client dùng list này để hiển thị icon.
    /// </summary>
    public NetworkList<ulong> CrownedNetworkIds;   // ulong = NetworkObjectId

    // ─── Server-only state ────────────────────────────────────────────────────
    private readonly Dictionary<TankPlayer, Action<Mau>> _deathHandlers =
        new Dictionary<TankPlayer, Action<Mau>>();

    private float _updateTimer;
    private bool _listHandlerRegistered;

    // ─── Singleton (optional convenience) ────────────────────────────────────
    public static BountySystem Instance { get; private set; }

    // ─── Events (Client) ─────────────────────────────────────────────────────
    /// <summary>Fired on all clients khi danh sách crown thay đổi.</summary>
    public static event Action OnCrownListChanged;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        CrownedNetworkIds = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsClient && !_listHandlerRegistered)
        {
            CrownedNetworkIds.OnListChanged += HandleCrownListChanged;
            _listHandlerRegistered = true;
        }

        if (!IsServer) { return; }

        // Subscribe players đã spawn trước
        TankPlayer[] existing = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer p in existing)
            SubscribePlayer(p);

        TankPlayer.OnPlayerSpawned   += SubscribePlayer;
        TankPlayer.OnPlayerDespawned += UnsubscribePlayer;

        if (IsClient)
        {
            OnCrownListChanged?.Invoke();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient && _listHandlerRegistered)
        {
            CrownedNetworkIds.OnListChanged -= HandleCrownListChanged;
            _listHandlerRegistered = false;
        }

        if (!IsServer) { return; }

        TankPlayer.OnPlayerSpawned   -= SubscribePlayer;
        TankPlayer.OnPlayerDespawned -= UnsubscribePlayer;

        foreach (var kvp in _deathHandlers)
        {
            if (kvp.Key != null && kvp.Key.Health != null)
                kvp.Key.Health.KhiChet -= kvp.Value;
        }
        _deathHandlers.Clear();

        if (Instance == this) { Instance = null; }
    }

    private void HandleCrownListChanged(NetworkListEvent<ulong> changeEvent)
    {
        OnCrownListChanged?.Invoke();
    }

    // ─── Update: refresh danh sách crown định kỳ ────────────────────────────
    private void Update()
    {
        if (!IsServer) { return; }

        _updateTimer += Time.deltaTime;
        if (_updateTimer < updateInterval) { return; }
        _updateTimer = 0f;

        RefreshCrownList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Kiểm tra client-side xem một NetworkObjectId có crown không.</summary>
    public bool HasCrown(ulong networkObjectId)
    {
        foreach (ulong id in CrownedNetworkIds)
        {
            if (id == networkObjectId) { return true; }
        }
        return false;
    }

    // ─── Server: quét và cập nhật crown list ─────────────────────────────────
    private void RefreshCrownList()
    {
        // Xây tập hợp id hiện tại xứng đáng có crown
        var shouldHaveCrown = new HashSet<ulong>();

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer p in players)
        {
            if (p == null || p.Wallet == null || !p.IsSpawned) { continue; }
            if (p.Wallet.TotalCoins.Value > bountyThreshold)
                shouldHaveCrown.Add(p.NetworkObjectId);
        }

        // Xoá những id không còn xứng đáng
        for (int i = CrownedNetworkIds.Count - 1; i >= 0; i--)
        {
            if (!shouldHaveCrown.Contains(CrownedNetworkIds[i]))
                CrownedNetworkIds.RemoveAt(i);
        }

        // Thêm id mới
        foreach (ulong id in shouldHaveCrown)
        {
            bool already = false;
            foreach (ulong existing in CrownedNetworkIds)
            {
                if (existing == id) { already = true; break; }
            }
            if (!already)
                CrownedNetworkIds.Add(id);
        }
    }

    // ─── Server: subscribe/unsubscribe death handler ─────────────────────────
    private void SubscribePlayer(TankPlayer player)
    {
        if (player == null || player.Health == null) { return; }
        if (_deathHandlers.ContainsKey(player)) { return; }

        Action<Mau> handler = health => HandlePlayerDeath(health, player);
        _deathHandlers[player] = handler;
        player.Health.KhiChet += handler;
    }

    private void UnsubscribePlayer(TankPlayer player)
    {
        if (player == null || player.Health == null) { return; }
        if (!_deathHandlers.TryGetValue(player, out var handler)) { return; }

        player.Health.KhiChet -= handler;
        _deathHandlers.Remove(player);

        // Xoá crown của player này ngay lập tức
        for (int i = CrownedNetworkIds.Count - 1; i >= 0; i--)
        {
            if (CrownedNetworkIds[i] == player.NetworkObjectId)
            {
                CrownedNetworkIds.RemoveAt(i);
                break;
            }
        }
    }

    // ─── Server: trao thưởng bounty khi xe có crown bị giết ──────────────────
    private void HandlePlayerDeath(Mau health, TankPlayer victim)
    {
        if (!IsServer || victim == null) { return; }

        // Chỉ xử lý khi nạn nhân đang có crown
        if (!HasCrown(victim.NetworkObjectId)) { return; }

        // Tìm kẻ giết
        if (!health.TryLayKeGiet(out TankPlayer killer)) { return; }
        if (killer == null || killer == victim) { return; }
        if (killer.Wallet == null || victim.Wallet == null) { return; }

        // Tính bounty = 20% coin của nạn nhân
        int victimCoins  = victim.Wallet.TotalCoins.Value;
        int bountyReward = Mathf.RoundToInt(victimCoins * (bountyRewardPercent / 100f));
        if (bountyReward <= 0) { return; }

        killer.Wallet.TotalCoins.Value += bountyReward;

        Debug.Log($"[BountySystem] {killer.PlayerName.Value} săn crown " +
                  $"{victim.PlayerName.Value} → nhận {bountyReward} coin bounty!");

        // Thông báo xuống client của kẻ giết
        NotifyBountyKillClientRpc(
            killer.PlayerName.Value.ToString(),
            victim.PlayerName.Value.ToString(),
            bountyReward,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { killer.OwnerClientId }
                }
            });
    }

    [ClientRpc]
    private void NotifyBountyKillClientRpc(
        string killerName,
        string victimName,
        int reward,
        ClientRpcParams rpcParams = default)
    {
        // Hiển thị thông báo nhỏ cho kẻ giết — dùng KillFeedClient hoặc UI riêng
        Debug.Log($"[Bounty] Bạn đã săn crown {victimName} và nhận {reward} coin!");
        BountyKillNotification.Show(victimName, reward);
    }
}
