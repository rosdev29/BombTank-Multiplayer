using UnityEngine;

public class BotCommand
{
    public Vector2  MoveInput  { get; set; } = Vector2.zero;
    public bool     Fire       { get; set; } = false;
    public Vector2? AimTarget  { get; set; } = null;
    public Vector2? PathDestination { get; set; } = null;
}
