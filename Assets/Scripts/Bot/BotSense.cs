using UnityEngine;

public class BotSense : MonoBehaviour
{
    [Header("Bán kính phát hiện")]
    [SerializeField] public float BanKinhPhatHienDich = 20f;
    [SerializeField] public float BanKinhPhatHienCoin = 20f;

    public void DocMoiTruong(BotContext ctx)
    {
        DocGiacQuanCoin(ctx);
        DocGiacQuanDich(ctx);
        DocGiacQuanHoiMau(ctx);
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

        foreach (TankPlayer p in TankPlayer.AllTankPlayers)
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

    // Tim vung hoi mau gan nhat; uu tien vung con nang luong, khong thi chon vung gan nhat de cho
    private void DocGiacQuanHoiMau(BotContext ctx)
    {
        ctx.NearestHealingZone    = null;
        ctx.DistanceToHealingZone = float.MaxValue;

        HealingZone ganNhatCoNangLuong = null;
        float       khoangCachCoNangLuong = float.MaxValue;
        HealingZone ganNhatBatKy = null;
        float       khoangCachBatKy = float.MaxValue;

        foreach (HealingZone zone in HealingZone.AllZones)
        {
            if (zone == null) { continue; }

            float dist = Vector2.Distance(ctx.BotPosition, zone.Position);
            if (dist < khoangCachBatKy)
            {
                khoangCachBatKy = dist;
                ganNhatBatKy    = zone;
            }

            if (!zone.CoTheHoiMau) { continue; }

            if (dist < khoangCachCoNangLuong)
            {
                khoangCachCoNangLuong = dist;
                ganNhatCoNangLuong    = zone;
            }
        }

        HealingZone chon = ganNhatCoNangLuong ?? ganNhatBatKy;
        if (chon == null) { return; }

        ctx.NearestHealingZone    = chon;
        ctx.HealingZonePosition   = chon.Position;
        ctx.DistanceToHealingZone = ganNhatCoNangLuong != null ? khoangCachCoNangLuong : khoangCachBatKy;
    }
}
