using System;
using System.Threading.Tasks;

namespace Unity.Services.Multiplay
{
    // Fallback stubs for projects that have not installed com.unity.services.multiplay yet.
    // Remove this file once the real Multiplay package is installed.
    public interface IMultiplayService
    {
        ServerConfig ServerConfig { get; }
        Task<IServerEvents> SubscribeToServerEventsAsync(MultiplayEventCallbacks callbacks);
        Task<IServerQueryHandler> StartServerQueryHandlerAsync(ushort maxPlayers, string serverName, string gameType, string buildId, string map);
        Task<T> GetPayloadAllocationFromJsonAs<T>();
    }

    public sealed class MultiplayService : IMultiplayService
    {
        private static readonly IMultiplayService s_instance = new MultiplayService();
        public static IMultiplayService Instance => s_instance;

        public ServerConfig ServerConfig { get; } = new ServerConfig();

        public Task<IServerEvents> SubscribeToServerEventsAsync(MultiplayEventCallbacks callbacks)
        {
            return Task.FromResult<IServerEvents>(new NullServerEvents());
        }

        public Task<IServerQueryHandler> StartServerQueryHandlerAsync(ushort maxPlayers, string serverName, string gameType, string buildId, string map)
        {
            return Task.FromResult<IServerQueryHandler>(new NullServerQueryHandler { MaxPlayers = maxPlayers, ServerName = serverName, GameType = gameType, BuildId = buildId, Map = map });
        }

        public Task<T> GetPayloadAllocationFromJsonAs<T>()
        {
            return Task.FromResult(default(T));
        }
    }

    public sealed class MultiplayEventCallbacks
    {
        public event Action<MultiplayAllocation> Allocate;
        public event Action<MultiplayDeallocation> Deallocate;
        public event Action<MultiplayError> Error;

        public void RaiseAllocate(MultiplayAllocation value) => Allocate?.Invoke(value);
        public void RaiseDeallocate(MultiplayDeallocation value) => Deallocate?.Invoke(value);
        public void RaiseError(MultiplayError value) => Error?.Invoke(value);
    }

    public interface IServerEvents
    {
        Task UnsubscribeAsync();
    }

    public interface IServerQueryHandler
    {
        string ServerName { get; set; }
        string BuildId { get; set; }
        ushort MaxPlayers { get; set; }
        ushort CurrentPlayers { get; set; }
        string Map { get; set; }
        string GameType { get; set; }
        void UpdateServerCheck();
    }

    public sealed class MultiplayAllocation
    {
        public string AllocationId { get; set; }
    }

    public sealed class MultiplayDeallocation
    {
        public string AllocationId { get; set; }
        public string EventId { get; set; }
        public string ServerId { get; set; }
    }

    public sealed class MultiplayError
    {
        public string Reason { get; set; }
        public string Detail { get; set; }
    }

    public sealed class ServerConfig
    {
        public string ServerId { get; set; }
        public string AllocationId { get; set; }
        public int Port { get; set; }
        public int QueryPort { get; set; }
        public string ServerLogDirectory { get; set; }
    }

    internal sealed class NullServerEvents : IServerEvents
    {
        public Task UnsubscribeAsync()
        {
            return Task.CompletedTask;
        }
    }

    internal sealed class NullServerQueryHandler : IServerQueryHandler
    {
        public string ServerName { get; set; }
        public string BuildId { get; set; }
        public ushort MaxPlayers { get; set; }
        public ushort CurrentPlayers { get; set; }
        public string Map { get; set; }
        public string GameType { get; set; }

        public void UpdateServerCheck()
        {
        }
    }
}
