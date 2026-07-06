using UnityEngine;

public class TrangThaiNhatCoin : IBotState
{
    private Vector2 _diemMucTieu;
    private float   _timerTimeout;
    private Coin    _coinDangDuoi; // Theo dõi coin hiện tại để reset timeout khi coin mới xuất hiện

    private const float KHOANG_CACH_CHAM   = 1.2f;
    private const float KHOANG_CACH_DA_TOI = 1.5f;
    private const float THOI_GIAN_TIMEOUT  = 8f;

    public void OnEnter(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemTuanTra(ctx);
        _timerTimeout = THOI_GIAN_TIMEOUT;
        _coinDangDuoi = ctx.NearestCoin;
    }

    public BotCommand Update(BotContext ctx)
    {
        if (ctx.NearestCoin != null)
        {
            // Reset timeout nếu đang theo đuổi coin mới (coin trước đã biến mất, xuất hiện coin khác)
            if (ctx.NearestCoin != _coinDangDuoi)
            {
                _timerTimeout = THOI_GIAN_TIMEOUT;
                _coinDangDuoi = ctx.NearestCoin;
            }

            float throttle = ctx.DistanceToCoin < KHOANG_CACH_CHAM ? 0.3f : 1f;
            if (ctx.Pathfinder != null)
                return ctx.Pathfinder.GetMoveCommandToTarget(ctx.CoinPosition, throttle);
            return BotSteering.MoveTowards(ctx, ctx.CoinPosition, throttle);
        }

        // Không có coin: tuần tra map
        _coinDangDuoi = null;
        _timerTimeout -= ctx.DeltaTime;

        if (BotSteering.DaToiNoi(ctx.BotPosition, _diemMucTieu, KHOANG_CACH_DA_TOI)
            || _timerTimeout <= 0f)
        {
            ChonDiemMoi(ctx);
        }

        if (ctx.Pathfinder != null)
            return ctx.Pathfinder.GetMoveCommandToTarget(_diemMucTieu);
        return BotSteering.MoveTowards(ctx, _diemMucTieu);
    }

    public void OnExit(BotContext ctx) { _coinDangDuoi = null; }

    private void ChonDiemMoi(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemTuanTra(ctx);
        _timerTimeout = THOI_GIAN_TIMEOUT;
    }

    /// <summary>
    /// Chọn điểm tuần tra: ưu tiên SpawnPoint xa nhất để phủ khắp map,
    /// fallback về vị trí ngẫu nhiên trong GridWorldSize nếu không có SpawnPoint.
    /// </summary>
    private static Vector2 ChonDiemTuanTra(BotContext ctx)
    {
        // Thử lấy SpawnPoint xa nhất so với vị trí hiện tại (tuần tra rộng hơn)
        Vector2 botPos = ctx.BotPosition;
        Vector2 xaNhat = botPos;
        float   maxDist = -1f;

        int soLanThu = 6; // Lấy 6 điểm ngẫu nhiên, chọn cái xa nhất để tuần tra rộng
        for (int i = 0; i < soLanThu; i++)
        {
            Vector2 p = SpawnPoint.GetRandomSpawnPos();
            float   d = Vector2.Distance(botPos, p);
            if (d > maxDist)
            {
                maxDist = d;
                xaNhat  = p;
            }
        }

        if (maxDist > 2f) return xaNhat;

        // Fallback: random trong grid (nếu không có SpawnPoint)
        PathfindingGrid grid = PathfindingGrid.Instance;
        if (grid != null)
        {
            Vector2 size = grid.GridWorldSize;
            return new Vector2(
                Random.Range(-size.x * 0.4f, size.x * 0.4f),
                Random.Range(-size.y * 0.4f, size.y * 0.4f)
            );
        }

        return SpawnPoint.GetRandomSpawnPos();
    }
}
