using UnityEngine;

public static class BotSteering
{
    public static BotCommand MoveTowards(BotContext ctx, Vector2 target, float throttle = 1f)
    {
        var cmd = new BotCommand();

        Vector2 huong = target - ctx.BotPosition;
        if (huong.sqrMagnitude < 0.001f) { return cmd; }

        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);
        cmd.MoveInput = new Vector2(gocLech > 0f ? -1f : 1f, throttle);

        return cmd;
    }

    public static bool DaToiNoi(Vector2 position, Vector2 target, float epsilon = 1.5f)
    {
        return Vector2.SqrMagnitude(target - position) <= epsilon * epsilon;
    }
}
