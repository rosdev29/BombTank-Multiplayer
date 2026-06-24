using UnityEngine;

public class TrangThaiNhatCoin : IBotState
{
    private Vector2 _diemMucTieu;
    private float   _timerTimeout;

    private const float KHOANG_CACH_CHAM   = 1.2f;
    private const float KHOANG_CACH_DA_TOI = 1.5f;
    private const float THOI_GIAN_TIMEOUT  = 8f;

    public void OnEnter(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemTrenMap();
        _timerTimeout = THOI_GIAN_TIMEOUT;
    }

    public BotCommand Update(BotContext ctx)
    {
        if (ctx.NearestCoin != null)
        {
            float throttle = ctx.DistanceToCoin < KHOANG_CACH_CHAM ? 0.3f : 1f;
            if (ctx.Pathfinder != null)
                return ctx.Pathfinder.GetMoveCommandToTarget(ctx.CoinPosition, throttle);
            return BotSteering.MoveTowards(ctx, ctx.CoinPosition, throttle);
        }

        if (BotSteering.DaToiNoi(ctx.BotPosition, _diemMucTieu, KHOANG_CACH_DA_TOI))
        {
            ChonDiemMoi();
        }
        else
        {
            _timerTimeout -= ctx.DeltaTime;
            if (_timerTimeout <= 0f)
                ChonDiemMoi();
        }

        if (ctx.Pathfinder != null)
            return ctx.Pathfinder.GetMoveCommandToTarget(_diemMucTieu);
        return BotSteering.MoveTowards(ctx, _diemMucTieu);
    }

    public void OnExit(BotContext ctx) { }

    private void ChonDiemMoi()
    {
        _diemMucTieu  = ChonDiemTrenMap();
        _timerTimeout = THOI_GIAN_TIMEOUT;
    }

    private static Vector2 ChonDiemTrenMap() => SpawnPoint.GetRandomSpawnPos();
}
