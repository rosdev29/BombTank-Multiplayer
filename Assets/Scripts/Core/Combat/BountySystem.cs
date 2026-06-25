using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bounty System — "Săn người giàu"
///
/// Quy tắc:
///   • Tại mỗi thời điểm CHỈ DUY NHẤT MỘT xe được mang bounty (crown 👑):
///     đó là xe đang dẫn đầu (TotalCoins cao nhất) VÀ vượt BountyThreshold.
///   • Nếu xe dẫn đầu bị loại (chết / disconnect) hoặc coin tụt xuống dưới
///     ngưỡng → bounty tự động reset; xe dẫn đầu mới (nếu đủ điều kiện) sẽ
///     được gắn crown ở lượt cập nhật kế tiếp.
///   • Khi xe đang mang crown bị giết → kẻ giết nhận thêm BountyRewardPercent%
///     coin của nạn nhân.
///
/// Server-side:
///   • Mỗi updateInterval giây, quét toàn bộ TankPlayer để tìm người dẫn đầu
///     và cập nhật lại danh sách crown (0 hoặc 1 phần tử).
///
/// Client-side:
///   • NetworkList<ulong> CrownedNetworkIds sync trạng thái crown xuống tất
///     cả client (giữ dạng list — thay vì NetworkVariable đơn — để không
///     phải sửa CrownDisplayClient/code client khác đang đọc theo list).
///   • CrownDisplayClient đọc list này để hiện/ẩn icon vương miện trên đầu xe.
/// </summary>
public class BountySystem : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Bounty Settings")]
    [Tooltip("Số coin tối thiểu mà xe dẫn đầu phải VƯỢT QUA để được tính là 'có bounty'.")]
    [SerializeField] private int bountyThreshold = 100;

    [Tooltip("Phần trăm coin của nạn nhân mà kẻ giết nhận thêm khi giết xe đang mang bounty.")]
    [SerializeField] private float bountyRewardPercent = 20f;

    [Tooltip("Bao lâu (giây) cập nhật lại người dẫn đầu 1 lần — 0.2s là đủ mịn.")]
    [SerializeField] private float updateInterval = 0.2f;

    // ─── Network State (sync Server → tất cả Client) ─────────────────────────
    /// <summary>
    /// Danh sách NetworkObjectId của xe đang có bounty.
    /// CHỈ CHỨA TỐI ĐA 1 PHẦN TỬ vì luật mới là "1 bounty duy nhất tại một
    /// thời điểm". Rỗng nghĩa là hiện không ai đủ điều kiện mang bounty.
    /// </summary>
    public NetworkList<ulong> CrownedNetworkIds;   // ulong = NetworkObjectId

    /// <summary>Tiện ích: true nếu hiện có xe đang mang bounty.</summary>
    public bool HasAnyBounty => CrownedNetworkIds.Count > 0;

    /// <summary>Tiện ích: NetworkObjectId của xe đang mang bounty, null nếu không có.</summary>
    public ulong? CurrentBountyHolderId =>
        CrownedNetworkIds.Count > 0 ? CrownedNetworkIds[0] : (ulong?)null;

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

    // ─── Update: refresh người dẫn đầu định kỳ ──────────────────────────────
    private void Update()
    {
        if (!IsServer) { return; }

        _updateTimer += Time.deltaTime;
        if (_updateTimer < updateInterval) { return; }
        _updateTimer = 0f;

        RefreshCrownList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Kiểm tra client-side xem một NetworkObjectId có đang mang bounty không.</summary>
    public bool HasCrown(ulong networkObjectId)
    {
        return CrownedNetworkIds.Count > 0 && CrownedNetworkIds[0] == networkObjectId;
    }

    // ─── Server: tìm xe dẫn đầu và cập nhật DUY NHẤT 1 crown (nếu đủ điều kiện) ─
    private void RefreshCrownList()
    {
        if (!CanModifyCrownList()) { return; }

        // Tìm xe có TotalCoins cao nhất trong số các xe đang sống/spawned
        TankPlayer leader = null;
        int leaderCoins = int.MinValue;

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer p in players)
        {
            if (p == null || p.Wallet == null || !p.IsSpawned) { continue; }

            int coins = p.Wallet.TotalCoins.Value;
            if (coins > leaderCoins)
            {
                leaderCoins = coins;
                leader = p;
            }
        }

        // Xe dẫn đầu chỉ thực sự "có bounty" nếu vượt ngưỡng quy định
        bool leaderQualifies = leader != null && leaderCoins > bountyThreshold;
        ulong newCrownId = leaderQualifies ? leader.NetworkObjectId : ulong.MaxValue;

        bool hasCurrentCrown = CrownedNetworkIds.Count > 0;
        ulong currentCrownId = hasCurrentCrown ? CrownedNetworkIds[0] : ulong.MaxValue;

        // Không có gì thay đổi → khỏi đụng vào NetworkList (tránh sync dư thừa)
        if (currentCrownId == newCrownId) { return; }

        if (hasCurrentCrown)
        {
            CrownedNetworkIds.Clear();
        }

        if (leaderQualifies)
        {
            CrownedNetworkIds.Add(newCrownId);
        }
    }

    // ─── Server: subscribe/unsubscribe death handler ─────────────────────────
    private bool CanModifyCrownList()
    {
        if (!IsServer || !IsSpawned) { return false; }
        if (NetworkManager == null || !NetworkManager.IsListening) { return false; }
        if (NetworkManager.ShutdownInProgress) { return false; }
        if (NetworkObject == null || !NetworkObject.IsSpawned) { return false; }
        return true;
    }

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
        // Không sửa CrownedNetworkIds ở đây — shutdown/despawn có thể làm NetworkList
        // không còn hợp lệ. RefreshCrownList() sẽ cập nhật crown ở tick kế tiếp.
    }

    // ─── Server: trao thưởng bounty khi xe đang mang crown bị giết ───────────
    private void HandlePlayerDeath(Mau health, TankPlayer victim)
    {
        if (!IsServer || victim == null) { return; }

        // Chỉ xử lý khi nạn nhân đang mang bounty (đang dẫn đầu)
        if (!HasCrown(victim.NetworkObjectId)) { return; }

        // Xe dẫn đầu bị loại → reset bounty ngay lập tức.
        // Lượt Update() kế tiếp (RefreshCrownList) sẽ tự tìm ra người dẫn đầu mới.
        if (CanModifyCrownList())
            CrownedNetworkIds.Clear();

        // Tìm kẻ giết
        if (!health.TryLayKeGiet(out TankPlayer killer)) { return; }
        if (killer == null || killer == victim) { return; }
        if (killer.Wallet == null || victim.Wallet == null) { return; }

        // Tính bounty = bountyRewardPercent% coin của nạn nhân
        int victimCoins  = victim.Wallet.TotalCoins.Value;
        int bountyReward = Mathf.RoundToInt(victimCoins * (bountyRewardPercent / 100f));
        if (bountyReward <= 0) { return; }

        killer.Wallet.TotalCoins.Value += bountyReward;
        killer.Wallet.LifetimeCoins.Value += bountyReward;

        Debug.Log($"[BountySystem] {killer.PlayerName.Value} săn bounty " +
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
        Debug.Log($"[Bounty] Bạn đã săn bounty {victimName} và nhận {reward} coin!");
        BountyKillNotification.Show(victimName, reward);
    }
}