using System;

/// <summary>
/// Hook for Thái's end-game screen later.
/// MatchTimer calls this when time runs out.
/// </summary>
public static class MatchEndBridge
{
    public static event Action OnMatchEnded;

    public static void NotifyMatchEnded()
    {
        OnMatchEnded?.Invoke();
    }
}
