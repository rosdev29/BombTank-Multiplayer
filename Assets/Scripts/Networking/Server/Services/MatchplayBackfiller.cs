using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class MatchplayBackfiller : IDisposable
{
    private CreateBackfillTicketOptions createBackfillOptions;
    private BackfillTicket localBackfillTicket;
    private bool localDataDirty;
    private int maxPlayers;
    private const int TicketCheckMs = 1000;
    private CancellationTokenSource backfillLoopCancelToken;
    private Task backfillLoopTask;
    private bool isDisposed;

    private int MatchPlayerCount => localBackfillTicket?.Properties.MatchProperties.Players.Count ?? 0;

    private MatchProperties MatchProperties => localBackfillTicket.Properties.MatchProperties;
    public bool IsBackfilling { get; private set; }

    public MatchplayBackfiller(string connection, string queueName, MatchProperties matchmakerPayloadProperties, int maxPlayers)
    {
        this.maxPlayers = maxPlayers;
        BackfillTicketProperties backfillProperties = new BackfillTicketProperties(matchmakerPayloadProperties);
        localBackfillTicket = new BackfillTicket
        {
            Id = matchmakerPayloadProperties.BackfillTicketId,
            Properties = backfillProperties
        };

        createBackfillOptions = new CreateBackfillTicketOptions
        {
            Connection = connection,
            QueueName = queueName,
            Properties = backfillProperties
        };
    }

    public async Task BeginBackfilling()
    {
        if (isDisposed)
        {
            Debug.LogWarning("Backfiller is disposed.");
            return;
        }

        if (IsBackfilling)
        {
            Debug.LogWarning("Already backfilling, no need to start another.");
            return;
        }

        Debug.Log($"Starting backfill Server: {MatchPlayerCount}/{maxPlayers}");

        if (string.IsNullOrEmpty(localBackfillTicket.Id))
        {
            localBackfillTicket.Id = await MatchmakerService.Instance.CreateBackfillTicketAsync(createBackfillOptions);
        }

        IsBackfilling = true;
        backfillLoopCancelToken?.Dispose();
        backfillLoopCancelToken = new CancellationTokenSource();

        backfillLoopTask = BackfillLoop(backfillLoopCancelToken.Token);
    }

    public void AddPlayerToMatch(UserData userData)
    {
        if (!IsBackfilling)
        {
            Debug.LogWarning("Can't add users to the backfill ticket before it's been created");
            return;
        }

        if (GetPlayerById(userData.userAuthId) != null)
        {
            Debug.LogWarningFormat("User: {0} - {1} already in Match. Ignoring add.",
                userData.userName,
                userData.userAuthId);
                
            return;
        }

        Player matchmakerPlayer = new Player(userData.userAuthId, userData.userGamePreferences);

        MatchProperties.Players.Add(matchmakerPlayer);
        MatchProperties.Teams[0].PlayerIds.Add(matchmakerPlayer.Id);
        localDataDirty = true;
    }

    public int RemovePlayerFromMatch(string userId)
    {
        Player playerToRemove = GetPlayerById(userId);
        if (playerToRemove == null)
        {
            Debug.LogWarning($"No user by the ID: {userId} in local backfill Data.");
            return MatchPlayerCount;
        }

        MatchProperties.Players.Remove(playerToRemove);
        MatchProperties.Teams[0].PlayerIds.Remove(userId);
        localDataDirty = true;

        return MatchPlayerCount;
    }

    public bool NeedsPlayers()
    {
        return MatchPlayerCount < maxPlayers;
    }

    public Team GetTeamByUserId(string userId)
    {
        if (MatchProperties?.Teams == null) { return null; }

        return MatchProperties.Teams.FirstOrDefault(
            t => t.PlayerIds != null && t.PlayerIds.Contains(userId));
    }

    private Player GetPlayerById(string userId)
    {
        return MatchProperties.Players.FirstOrDefault(
            p => p.Id.Equals(userId));
    }

    public async Task StopBackfill()
    {
        if (!IsBackfilling)
        {
            return;
        }

        IsBackfilling = false;
        if (backfillLoopCancelToken != null && !backfillLoopCancelToken.IsCancellationRequested)
        {
            backfillLoopCancelToken.Cancel();
        }

        if (!string.IsNullOrEmpty(localBackfillTicket.Id))
        {
            try
            {
                await MatchmakerService.Instance.DeleteBackfillTicketAsync(localBackfillTicket.Id);
            }
            catch (MatchmakerServiceException e)
            {
                Debug.LogWarning($"DeleteBackfillTicketAsync failed for {localBackfillTicket.Id}: {e}");
            }
            finally
            {
                localBackfillTicket.Id = null;
            }
        }

        if (backfillLoopTask != null)
        {
            try
            {
                await backfillLoopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected while stopping the backfill loop.
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Backfill loop stopped with exception: {e}");
            }
            finally
            {
                backfillLoopTask = null;
            }
        }
    }

    private async Task BackfillLoop(CancellationToken cancellationToken)
    {
        while (IsBackfilling && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (localDataDirty)
                {
                    await MatchmakerService.Instance.UpdateBackfillTicketAsync(localBackfillTicket.Id, localBackfillTicket);
                    localDataDirty = false;
                }
                else
                {
                    localBackfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(localBackfillTicket.Id);
                }
            }
            catch (MatchmakerServiceException e)
            {
                Debug.LogWarning($"Backfill loop request failed: {e}");
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!NeedsPlayers())
            {
                IsBackfilling = false;
                if (!string.IsNullOrEmpty(localBackfillTicket.Id))
                {
                    try
                    {
                        await MatchmakerService.Instance.DeleteBackfillTicketAsync(localBackfillTicket.Id);
                    }
                    catch (MatchmakerServiceException e)
                    {
                        Debug.LogWarning($"DeleteBackfillTicketAsync failed for {localBackfillTicket.Id}: {e}");
                    }
                    finally
                    {
                        localBackfillTicket.Id = null;
                    }
                }
                break;
            }

            await Task.Delay(TicketCheckMs, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (isDisposed) { return; }
        isDisposed = true;

        if (backfillLoopCancelToken != null && !backfillLoopCancelToken.IsCancellationRequested)
        {
            backfillLoopCancelToken.Cancel();
        }
        backfillLoopCancelToken?.Dispose();
        backfillLoopCancelToken = null;

        IsBackfilling = false;
    }
}