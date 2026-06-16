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

        cmd.PathDestination = targetPos;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
