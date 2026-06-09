using UnityEngine;

public class BotSense : MonoBehaviour
{
    [Header("Bán kính phát hiện")]
    [SerializeField] public float BanKinhPhatHienDich = 15f;
    [SerializeField] public float BanKinhPhatHienCoin = 20f;
    [SerializeField] public float BanKinhPhatHienItem = 25f;

    public void DocMoiTruong(BotContext ctx)
    {
        DocGiacQuanCoin(ctx);
        DocGiacQuanDich(ctx);
        DocGiacQuanItem(ctx);
    }

    private void DocGiacQuanCoin(BotContext ctx)
    {
        ctx.NearestCoin    = null;
        ctx.DistanceToCoin = float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.BotPosition, BanKinhPhatHienCoin);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) { continue; }
            Coin coin = hit.GetComponent<Coin>();
            if (coin == null) { continue; }

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

    private void DocGiacQuanItem(BotContext ctx)
    {
        ctx.NearestItem    = null;
        ctx.DistanceToItem = float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.BotPosition, BanKinhPhatHienItem);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) { continue; }
            ItemPickup item = hit.GetComponent<ItemPickup>();
            if (item == null) { continue; }

            float dist = Vector2.Distance(ctx.BotPosition, (Vector2)item.transform.position);
            if (dist < ctx.DistanceToItem)
            {
                ctx.DistanceToItem = dist;
                ctx.NearestItem    = item;
                ctx.ItemPosition   = (Vector2)item.transform.position;
            }
        }
    }

    private void DocGiacQuanDich(BotContext ctx)
    {
        ctx.NearestEnemy    = null;
        ctx.DistanceToEnemy = float.MaxValue;
        ctx.DanhSachDichGan.Clear();

        foreach (TankPlayer p in BotBrain.AllPlayers)
        {
            if (p == null || p == ctx.Player) { continue; }
            if (p.TeamIndex.Value == ctx.Player.TeamIndex.Value) { continue; }

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
