using UnityEngine;

public class TrangThaiNhatCoin : IBotState
{
    public void OnEnter(BotContext ctx) { }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        if (ctx.NearestCoin == null) { return cmd; }

        Vector2 huong = ctx.CoinPosition - ctx.BotPosition;
        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);

        cmd.MoveInput = new Vector2(gocLech > 0 ? -1f : 1f, 1f);

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
