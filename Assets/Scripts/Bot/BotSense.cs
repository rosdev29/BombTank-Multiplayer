using UnityEngine;

public class BotSense : MonoBehaviour
{
    [Header("Bán kính phát hiện")]
    [SerializeField] public float BanKinhPhatHienDich = 15f;
    [SerializeField] public float BanKinhPhatHienCoin = 20f;

    public void DocMoiTruong(BotContext ctx)
    {
        DocGiacQuanCoin(ctx);
        DocGiacQuanDich(ctx);
    }

    private void DocGiacQuanCoin(BotContext ctx)
    {
        ctx.NearestCoin     = null;
        ctx.DistanceToCoin  = float.MaxValue;
        ctx.DanhSachCoinGan.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.BotPosition, BanKinhPhatHienCoin);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) { continue; }
            Coin coin = hit.GetComponent<Coin>();
            if (coin == null) { continue; }

            ctx.DanhSachCoinGan.Add(coin);

            float dist = Vector2.Distance(ctx.BotPosition, (Vector2)coin.transform.position);
            if (dist < ctx.DistanceToCoin)
            {
                ctx.DistanceToCoin = dist;
                ctx.NearestCoin    = coin;
                ctx.CoinPosition   = (Vector2)coin.transform.position;
            }
        }

        ctx.SoCoinHienTai = ctx.Wallet != null ? ctx.Wallet.TotalCoins.Value : 0;
    }

    private void DocGiacQuanDich(BotContext ctx)
    {
        ctx.NearestEnemy    = null;
        ctx.DistanceToEnemy = float.MaxValue;
        ctx.DanhSachDichGan.Clear();

        foreach (TankPlayer p in BotBrain.AllPlayers)
        {
            if (p == null || p == ctx.Player) { continue; }

            // Bỏ qua đồng đội: TeamIndex >= 0 và bằng nhau (người thật cùng team).
            // Bot có TeamIndex = -1 → coi tất cả xe khác (kể cả bot khác) là địch.
            bool cungTeam = ctx.Player.TeamIndex.Value >= 0
                         && p.TeamIndex.Value == ctx.Player.TeamIndex.Value;
            if (cungTeam) { continue; }

            float dist = Vector2.Distance(ctx.BotPosition, (Vector2)p.transform.position);
            if (dist > BanKinhPhatHienDich) { continue; }

            ctx.DanhSachDichGan.Add(p);

            if (dist < ctx.DistanceToEnemy)
            {
                ctx.DistanceToEnemy = dist;
                ctx.NearestEnemy    = p;
                ctx.EnemyPosition   = (Vector2)p.transform.position;
            }
        }
    }
}
