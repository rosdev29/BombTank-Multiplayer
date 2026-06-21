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

<<<<<<< HEAD
        float throttle = ctx.DistanceToCoin < KHOANG_CACH_CHAM ? 0.3f : 1f;
        return BotSteering.MoveTowards(ctx, ctx.CoinPosition, throttle);
=======
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
>>>>>>> origin/item
    }

    public void OnExit(BotContext ctx) { }
}
