/// <summary>
/// Blocks local player gameplay input (move, aim, shoot, items) during match-end overlay.
/// </summary>
public static class GameplayInputGate
{
    private static bool manualBlocked;

    public static bool IsBlocked => manualBlocked || MatchEndBridge.IsMatchEnded;

    public static void SetBlocked(bool blocked)
    {
        manualBlocked = blocked;
    }
}
