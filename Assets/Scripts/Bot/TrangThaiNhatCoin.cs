using UnityEngine;

public class TrangThaiNhatCoin : IBotState
{
    public void OnEnter(BotContext ctx) { }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        Vector2 targetPos = ctx.BotPosition;
        if (ctx.NearestItem != null)
        {
            targetPos = ctx.ItemPosition;
        }
        else if (ctx.NearestCoin != null)
        {
            targetPos = ctx.CoinPosition;
        }
        else
        {
            return cmd;
        }

        Vector2 huong = targetPos - ctx.BotPosition;
        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);

        cmd.MoveInput = new Vector2(gocLech > 0 ? -1f : 1f, 1f);

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
