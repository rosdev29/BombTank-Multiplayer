using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public enum MatchmakerPollingResult
{
    Success,
    TicketCreationError,
    TicketCancellationError,
    TicketRetrievalError,
    MatchAssignmentError
}

public class MatchmakingResult
{
    public string ip;
    public int port;
    public MatchmakerPollingResult result;
    public string resultMessage;
}

public class MatchplayMatchmaker : IDisposable
{
    private string lastUsedTicket;
    private CancellationTokenSource cancelToken;
    private bool isDisposed;

    private const int TicketCooldown = 1000;

    public bool IsMatchmaking { get; private set; }

    public async Task<MatchmakingResult> Matchmake(UserData data)
    {
        if (isDisposed)
        {
            return ReturnMatchResult(MatchmakerPollingResult.TicketCreationError, "Matchmaker is disposed.", null);
        }

        cancelToken?.Dispose();
        cancelToken = new CancellationTokenSource();

        string queueName = data.userGamePreferences.ToMultiplayQueue();
        CreateTicketOptions createTicketOptions = new CreateTicketOptions(queueName);
        Debug.Log(createTicketOptions.QueueName);

        List<Player> players = new List<Player>
        {
            new Player(data.userAuthId, data.userGamePreferences)
        };

        try
        {
            IsMatchmaking = true;
            CreateTicketResponse createResult = await MatchmakerService.Instance.CreateTicketAsync(players, createTicketOptions);

            lastUsedTicket = createResult.Id;

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    TicketStatusResponse checkTicket = await MatchmakerService.Instance.GetTicketAsync(lastUsedTicket);

                    if (checkTicket.Type == typeof(MultiplayAssignment))
                    {
                        MultiplayAssignment matchAssignment = (MultiplayAssignment)checkTicket.Value;

                        if (matchAssignment.Status == MultiplayAssignment.StatusOptions.Found)
                        {
                            return ReturnMatchResult(MatchmakerPollingResult.Success, "", matchAssignment);
                        }
                        if (matchAssignment.Status == MultiplayAssignment.StatusOptions.Timeout ||
                            matchAssignment.Status == MultiplayAssignment.StatusOptions.Failed)
                        {
                            return ReturnMatchResult(MatchmakerPollingResult.MatchAssignmentError,
                                $"Ticket: {lastUsedTicket} - {matchAssignment.Status} - {matchAssignment.Message}", null);
                        }
                        Debug.Log($"Polled Ticket: {lastUsedTicket} Status: {matchAssignment.Status} ");
                    }

                    await Task.Delay(TicketCooldown, cancelToken.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return ReturnMatchResult(MatchmakerPollingResult.TicketRetrievalError, "Cancelled Matchmaking", null);
            }
            catch (MatchmakerServiceException e)
            {
                return ReturnMatchResult(MatchmakerPollingResult.TicketRetrievalError, e.ToString(), null);
            }
        }
        catch (MatchmakerServiceException e)
        {
            return ReturnMatchResult(MatchmakerPollingResult.TicketCreationError, e.ToString(), null);
        }

        return ReturnMatchResult(MatchmakerPollingResult.TicketRetrievalError, "Cancelled Matchmaking", null);
    }

    public async Task CancelMatchmaking()
    {
        if (!IsMatchmaking || cancelToken == null) { return; }

        IsMatchmaking = false;

        if (!cancelToken.IsCancellationRequested && cancelToken.Token.CanBeCanceled)
        {
            cancelToken.Cancel();
        }

        if (string.IsNullOrEmpty(lastUsedTicket)) { return; }

        Debug.Log($"Cancelling {lastUsedTicket}");

        try
        {
            await MatchmakerService.Instance.DeleteTicketAsync(lastUsedTicket);
        }
        catch (MatchmakerServiceException e)
        {
            Debug.LogWarning($"Failed to cancel ticket {lastUsedTicket}: {e}");
        }
        finally
        {
            lastUsedTicket = string.Empty;
        }
    }

    private MatchmakingResult ReturnMatchResult(MatchmakerPollingResult resultErrorType, string message, MultiplayAssignment assignment)
    {
        IsMatchmaking = false;

        if (assignment != null)
        {
            string parsedIp = assignment.Ip;
            int? parsedPort = assignment.Port;
            if (parsedPort == null)
            {
                return new MatchmakingResult
                {
                    result = MatchmakerPollingResult.MatchAssignmentError,
                    resultMessage = $"Port missing? - {assignment.Port}\n-{assignment.Message}"
                };
            }

            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.Success,
                ip = parsedIp,
                port = (int)parsedPort,
                resultMessage = assignment.Message
            };
        }

        return new MatchmakingResult
        {
            result = resultErrorType,
            resultMessage = message
        };
    }

    public void Dispose()
    {
        if (isDisposed) { return; }
        isDisposed = true;

        if (cancelToken != null)
        {
            if (!cancelToken.IsCancellationRequested)
            {
                cancelToken.Cancel();
            }
            cancelToken.Dispose();
            cancelToken = null;
        }

        lastUsedTicket = string.Empty;
    }
}