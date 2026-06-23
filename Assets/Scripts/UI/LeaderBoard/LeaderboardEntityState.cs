using System;
using Unity.Collections;
using Unity.Netcode;

public struct LeaderboardEntityState : INetworkSerializable, IEquatable<LeaderboardEntityState>
{
    public ulong ClientId;
    public int TeamIndex;
    public FixedString32Bytes PlayerName;
    public int Coins;
  /// <summary>RTT in ms for human clients. -1 = bot / unavailable.</summary>
    public int PingMs;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamIndex);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref Coins);
        serializer.SerializeValue(ref PingMs);
    }

    public bool Equals(LeaderboardEntityState other)
    {
        return ClientId == other.ClientId &&
               TeamIndex == other.TeamIndex &&
               PlayerName.Equals(other.PlayerName) &&
               Coins == other.Coins &&
               PingMs == other.PingMs;
    }
}
