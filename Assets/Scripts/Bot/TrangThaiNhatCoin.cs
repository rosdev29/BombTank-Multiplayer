using UnityEngine;

public class TrangThaiNhatCoin : IBotState
{
    private const float KHOANG_CACH_CHAM = 1.2f;
    private const int   CHI_PHI_BAN      = 1;

    public void OnEnter(BotContext ctx) { }

    public BotCommand Update(BotContext ctx)
    {
        if (ctx.NearestCoin == null || ctx.DuCoinDeBan(CHI_PHI_BAN))
        {
            return new BotCommand();
        }

        float throttle = ctx.DistanceToCoin < KHOANG_CACH_CHAM ? 0.3f : 1f;
        if (ctx.Pathfinder != null)
        {
            return ctx.Pathfinder.GetMoveCommandToTarget(ctx.CoinPosition, throttle);
        }
        return BotSteering.MoveTowards(ctx, ctx.CoinPosition, throttle);
    }

    public void OnExit(BotContext ctx) { }
}
