using System;

/// <summary>
/// Hook for Thái's end-game screen later.
/// MatchTimer calls this when time runs out.
/// </summary>
public static class MatchEndBridge
{
    public static bool IsMatchEnded { get; private set; }
    public static event Action OnMatchEnded;

    public static void NotifyMatchEnded()
    {
        if (IsMatchEnded) { return; }

        IsMatchEnded = true;
        OnMatchEnded?.Invoke();
    }

    public static void Reset()
    {
        IsMatchEnded = false;
    }
}
